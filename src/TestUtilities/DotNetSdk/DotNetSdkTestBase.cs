// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace TestUtilities
{
    public abstract partial class DotNetSdkTestBase : IDisposable
    {
        public sealed class DotNetSdkAvailable : ExecutionCondition
        {
            public override bool ShouldSkip => s_dotnetSdkPath == null;
            public override string SkipReason => "The location of dotnet SDK can't be determined (DOTNET_INSTALL_DIR environment variable is unset)";
        }

        public readonly TempRoot Temp = new TempRoot();

        private static readonly string s_dotnetExeName;
        private static readonly string s_dotnetInstallDir;
        private static readonly BuildInfoAttribute s_buildInfo;
        private static readonly string s_dotnetSdkPath;

        private static string s_projectSource =
@"<Project Sdk='Microsoft.NET.Sdk'>
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
</Project>
";
        private static string s_classSource =
@"using System;

public class TestClass 
{
    public void F() 
    {
        Console.WriteLine(123);
    }
}
";
        private static string GetLocalNuGetConfigContent(string packagesDir) =>
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <clear/>
    <add key=""local_packages"" value=""{packagesDir}"" />
  </packageSources>
</configuration>
";

        protected readonly TempDirectory ProjectDir;
        protected readonly TempDirectory ObjDir;
        protected readonly TempDirectory NuGetCacheDir;
        protected readonly TempDirectory OutDir;
        protected readonly TempFile Project;
        protected readonly string ProjectSourceRoot;
        protected readonly string ProjectName;
        protected readonly string ProjectFileName;
        protected readonly string Configuration;
        protected readonly string TargetFramework;
        protected readonly string DotNetPath;
        protected readonly IReadOnlyDictionary<string, string> EnvironmentVariables;

        protected static readonly string s_relativeSourceLinkJsonPath = Path.Combine("obj", "Debug", "netstandard2.0", "test.sourcelink.json");
        protected static readonly string s_relativeOutputFilePath = Path.Combine("obj", "Debug", "netstandard2.0", "test.dll");
        protected static readonly string s_relativePackagePath = Path.Combine("bin", "Debug", "test.1.0.0.nupkg");

        private int _logIndex;

        private static string GetSdkPath(string dotnetInstallDir, string version)
            => Path.Combine(dotnetInstallDir, "sdk", version);

        static DotNetSdkTestBase()
        {
            s_dotnetExeName = "dotnet" + (Path.DirectorySeparatorChar == '/' ? "" : ".exe");
            s_buildInfo = typeof(DotNetSdkTestBase).Assembly.GetCustomAttribute<BuildInfoAttribute>();

            bool isMatchingDotNetInstance(string dotnetDir)
                => dotnetDir != null && File.Exists(Path.Combine(dotnetDir, s_dotnetExeName)) && Directory.Exists(GetSdkPath(dotnetDir, s_buildInfo.SdkVersion));

            var dotnetInstallDir = Environment.GetEnvironmentVariable("DOTNET_INSTALL_DIR");
            if (!isMatchingDotNetInstance(dotnetInstallDir))
            {
                dotnetInstallDir = Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator).FirstOrDefault(isMatchingDotNetInstance);
            }

            if (dotnetInstallDir != null)
            {
                s_dotnetInstallDir = dotnetInstallDir;
                s_dotnetSdkPath = GetSdkPath(dotnetInstallDir, s_buildInfo.SdkVersion);
            }
        }

        public void Dispose() 
            => Temp.Dispose();

        private const string EmptyValueMarker = "--{empty}--";

        private static void EmitTestHelperProps(
            string objDirectory,
            string projectFileName,
            string content)
        {
            // Common.props automatically import {project-name}.*.props files from MSBuildProjectExtensionsPath directory, 
            // which is by default set to the IntermediateOutputPath:
            File.WriteAllText(Path.Combine(objDirectory, projectFileName + ".TestHelpers.g.props"),
$@"<Project>
{content}
</Project>");
        }

        private static void EmitTestHelperTargets(
            string objDirectory,
            string outputFile,
            string projectFileName,
            IEnumerable<string> expressions,
            string additionalContent)
        {
            // Common.targets automatically import {project-name}.*.targets files from MSBuildProjectExtensionsPath directory, 
            // which is by defautl set to the IntermediateOutputPath:
            File.WriteAllText(Path.Combine(objDirectory, projectFileName + ".TestHelpers.g.targets"),
$@"<Project>      
  <Target Name=""Test_EvaluateExpressions"">
    <PropertyGroup>
      {string.Join(Environment.NewLine + "      ", expressions.SelectWithIndex((e, i) => $@"<_Value{i}>{e}</_Value{i}><_Value{i} Condition=""'$(_Value{i})' == ''"">{EmptyValueMarker}</_Value{i}>"))}
    </PropertyGroup>
    <ItemGroup>
      <LinesToWrite Include=""{string.Join(";", expressions.SelectWithIndex((e, i) => $"$(_Value{i})"))}""/>
    </ItemGroup>
    <MakeDir Directories=""{Path.GetDirectoryName(outputFile)}"" />
    <WriteLinesToFile File=""{outputFile}""
                      Lines=""@(LinesToWrite)""
                      Overwrite=""true""
                      Encoding=""UTF-8"" />
  </Target>
{additionalContent}
</Project>");
        }

        public DotNetSdkTestBase(params string[] packages)
        {
            Assert.True(s_dotnetInstallDir != null, $"SDK not found. Use {nameof(ConditionalFactAttribute)}(typeof({nameof(DotNetSdkAvailable)})) to skip the test if the SDK is not found.");

            DotNetPath = Path.Combine(s_dotnetInstallDir, s_dotnetExeName);
            var testBinDirectory = Path.GetDirectoryName(typeof(DotNetSdkTestBase).Assembly.Location);
            var sdksDir = Path.Combine(s_dotnetSdkPath, "Sdks");

            ProjectName = "test";
            ProjectFileName = ProjectName + ".csproj";
            Configuration = "Debug";
            TargetFramework = "netstandard2.0";

            ProjectDir = Temp.CreateDirectory();
            ProjectSourceRoot = ProjectDir.Path + Path.DirectorySeparatorChar;
            NuGetCacheDir = ProjectDir.CreateDirectory(".packages");
            ObjDir = ProjectDir.CreateDirectory("obj");
            OutDir = ProjectDir.CreateDirectory("bin").CreateDirectory(Configuration).CreateDirectory(TargetFramework);

            Project = ProjectDir.CreateFile(ProjectFileName).WriteAllText(s_projectSource);
            ProjectDir.CreateFile("TestClass.cs").WriteAllText(s_classSource);

            ProjectDir.CreateFile("Directory.Build.props").WriteAllText(
$@"<Project>
  <ItemGroup>
    {string.Join(Environment.NewLine, packages.Select(packageName => $"<PackageReference Include='{packageName}' Version='1.0.0-*' PrivateAssets='all' />"))}
  </ItemGroup>
</Project>
");
            ProjectDir.CreateFile("Directory.Build.targets").WriteAllText("<Project/>");
            ProjectDir.CreateFile(".editorconfig").WriteAllText("root = true");
            ProjectDir.CreateFile("nuget.config").WriteAllText(GetLocalNuGetConfigContent(s_buildInfo.PackagesDirectory));

            EnvironmentVariables = new Dictionary<string, string>()
            {
                { "MSBuildSDKsPath", sdksDir },
                { "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR", sdksDir },
                { "NUGET_PACKAGES", NuGetCacheDir.Path }
            };

            var restoreResult = ProcessUtilities.Run(DotNetPath, $@"msbuild ""{Project.Path}"" /t:restore /bl:{Path.Combine(ProjectDir.Path, "restore.binlog")}",
                additionalEnvironmentVars: EnvironmentVariables);
            Assert.True(restoreResult.ExitCode == 0, $"Failed with exit code {restoreResult.ExitCode}: {restoreResult.Output}");

            Assert.True(File.Exists(Path.Combine(ObjDir.Path, "project.assets.json")));
            Assert.True(File.Exists(Path.Combine(ObjDir.Path, ProjectFileName + ".nuget.g.props")));
            Assert.True(File.Exists(Path.Combine(ObjDir.Path, ProjectFileName + ".nuget.g.targets")));
        }

        protected void VerifyValues(string customProps, string customTargets, string[] targets, string[] expressions, string[] expectedResults = null, string[] expectedErrors = null, string[] expectedWarnings = null)
        {
            Debug.Assert(targets != null);
            Debug.Assert(expressions != null);
            Debug.Assert(expectedResults == null ^ expectedErrors == null);

            var evaluationResultsFile = Path.Combine(OutDir.Path, "EvaluationResult.txt");

            EmitTestHelperProps(ObjDir.Path, ProjectFileName, customProps);
            EmitTestHelperTargets(ObjDir.Path, evaluationResultsFile, ProjectFileName, expressions, customTargets);

            var targetsArg = string.Join(";", targets.Concat(new[] { "Test_EvaluateExpressions" }));
            var testBinDirectory = Path.GetDirectoryName(typeof(DotNetSdkTestBase).Assembly.Location);
            var buildLog = Path.Combine(ProjectDir.Path, $"build{_logIndex++}.binlog");
            var restoreLog = Path.Combine(ProjectDir.Path, $"restore{_logIndex++}.binlog");

            var buildResult = ProcessUtilities.Run(DotNetPath, $@"msbuild ""{Project.Path}"" /t:{targetsArg} /p:Configuration={Configuration} /bl:""{buildLog}""",
                additionalEnvironmentVars: EnvironmentVariables);

            string[] getDiagnostics(string[] lines, bool error)
                => (from line in lines
                    let match = Regex.Match(line, $@"^.*\([0-9]+,[0-9]+\): {(error ? "error" : "warning")} : (.*) \[.*\]$")
                    where match.Success
                    select match.Groups[1].Value).ToArray();

            bool diagnosticsEqual(string expected, string actual)
            {
                string ellipsis = "...";
                int index = expected.IndexOf(ellipsis);
                return (index == -1) ? expected == actual :
                    actual.Length > expected.Length - ellipsis.Length &&
                    expected.Substring(0, index) == actual.Substring(0, index) && 
                    expected.Substring(index + ellipsis.Length) == actual.Substring(actual.Length - (expected.Length - index - ellipsis.Length));
            }

            var outputLines = buildResult.Output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            if (expectedErrors == null)
            {
                Assert.True(buildResult.ExitCode == 0, $"Build failed with exit code {buildResult.ExitCode}: {buildResult.Output}");

                var evaluationResult = File.ReadAllLines(evaluationResultsFile).Select(l => (l != EmptyValueMarker) ? l : "");
                AssertEx.Equal(expectedResults, evaluationResult);
            }
            else
            {
                Assert.True(buildResult.ExitCode != 0, $"Build succeeded but should have failed: {buildResult.Output}");

                var actualErrors = getDiagnostics(outputLines, error: true);
                AssertEx.Equal(expectedErrors, actualErrors, diagnosticsEqual);
            }

            var actualWarnings = getDiagnostics(outputLines, error: false);
            AssertEx.Equal(expectedWarnings ?? Array.Empty<string>(), actualWarnings, diagnosticsEqual);
        }
    }
}
