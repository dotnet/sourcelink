// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using NuGet.Versioning;
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
        private static readonly string? s_dotnetInstallDir;
        private static readonly BuildInfoAttribute s_buildInfo;
        private static readonly string? s_dotnetSdkPath;

        private static readonly string s_projectSource =
@"<Project Sdk='Microsoft.NET.Sdk'>
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
</Project>
";
        private static readonly string s_classSource =
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
    <add key=""nuget"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""local_packages"" value=""{packagesDir}"" />
  </packageSources>
</configuration>
";

        protected readonly TempDirectory RootDir;
        protected readonly TempDirectory ProjectDir;
        protected readonly TempDirectory ProjectObjDir;
        protected readonly TempDirectory NuGetCacheDir;
        protected readonly string NuGetPackageFolders;
        protected readonly TempDirectory ProjectOutDir;
        protected readonly TempFile Project;
        protected readonly string SourceRoot;
        protected readonly string ProjectSourceRoot;
        protected readonly string ProjectName;
        protected readonly string ProjectFileName;
        protected readonly string Configuration;
        protected readonly string TargetFramework;
        protected readonly string DotNetPath;
        protected readonly Dictionary<string, string> EnvironmentVariables;

        protected static readonly string s_relativeSourceLinkJsonPath = Path.Combine("obj", "Debug", "netstandard2.0", "test.sourcelink.json");
        protected static readonly string s_relativeOutputFilePath = Path.Combine("obj", "Debug", "netstandard2.0", "test.dll");
        protected static readonly string s_relativePackagePath = Path.Combine("bin", "Debug", "test.1.0.0.nupkg");

        private bool _projectRestored;
        private int _logIndex;

        static DotNetSdkTestBase()
        {
            s_dotnetExeName = "dotnet" + (Path.DirectorySeparatorChar == '/' ? "" : ".exe");
            s_buildInfo = typeof(DotNetSdkTestBase).Assembly!.GetCustomAttribute<BuildInfoAttribute>()!;

            var minSdkVersion = SemanticVersion.Parse(s_buildInfo.SdkVersion);

            static bool isDotNetInstallDirectory(string? dir)
                => dir != null && File.Exists(Path.Combine(dir, s_dotnetExeName));

            var dotnetInstallDir =
                new[] { Environment.GetEnvironmentVariable("DOTNET_INSTALL_DIR") }.Concat(Environment.GetEnvironmentVariable("PATH")!.Split(Path.PathSeparator)).
                FirstOrDefault(isDotNetInstallDirectory);

            if (dotnetInstallDir != null)
            {
                foreach (var dir in Directory.EnumerateDirectories(Path.Combine(dotnetInstallDir, "sdk")))
                {
                    var versionDir = Path.GetFileName(dir);
                    if (SemanticVersion.TryParse(versionDir, out var version) && version >= minSdkVersion)
                    {
                        s_dotnetInstallDir = dotnetInstallDir;
                        s_dotnetSdkPath = Path.Combine(dotnetInstallDir, "sdk", versionDir);
                        break;
                    }
                }
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
    <Message Text=""@(SourceRoot->'%(SourceLinkUrl)')"" />
    <PropertyGroup>
{string.Join(Environment.NewLine, expressions.SelectWithIndex((e, i) => $@"      <_Value{i}>{e}</_Value{i}><_Value{i} Condition=""'$(_Value{i})' == ''"">{EmptyValueMarker}</_Value{i}>"))}
    </PropertyGroup>
    <ItemGroup>
{string.Join(Environment.NewLine, expressions.SelectWithIndex((e, i) => $@"      <LinesToWrite Include=""$(_Value{i})""/>"))}
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

            DotNetPath = Path.Combine(s_dotnetInstallDir!, s_dotnetExeName);
            var testBinDirectory = Path.GetDirectoryName(typeof(DotNetSdkTestBase).Assembly.Location);
            var sdksDir = Path.Combine(s_dotnetSdkPath!, "Sdks");

            ProjectName = "test";
            ProjectFileName = ProjectName + ".csproj";
            Configuration = "Debug";
            TargetFramework = "netstandard2.0";

            RootDir = Temp.CreateDirectory();
            NuGetCacheDir = RootDir.CreateDirectory(".packages");
            NuGetPackageFolders = EnsureTrailingDirectorySeparator(NuGetCacheDir.Path);

            // {info-version} = {package-version}+{commit-sha}
            var packageVersion = typeof(DotNetSdkTestBase).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

            RootDir.CreateFile("Directory.Build.props").WriteAllText(
$@"<Project>
  <ItemGroup>
    {string.Join(Environment.NewLine, packages.Select(packageName => $"<PackageReference Include='{packageName}' Version='{packageVersion}' PrivateAssets='all' />"))}
  </ItemGroup>
</Project>
");
            RootDir.CreateFile("Directory.Build.targets").WriteAllText("<Project/>");
            RootDir.CreateFile(".editorconfig").WriteAllText("root = true");
            RootDir.CreateFile("nuget.config").WriteAllText(GetLocalNuGetConfigContent(s_buildInfo.PackagesDirectory));

            SourceRoot = RootDir.Path + Path.DirectorySeparatorChar;

            EnvironmentVariables = new Dictionary<string, string>()
            {
                { "MSBuildSDKsPath", sdksDir },
                { "DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR", sdksDir },
                { "NUGET_PACKAGES", NuGetCacheDir.Path },
                { "NuGetPackageFolders", NuGetPackageFolders },
            };

            ProjectDir = RootDir.CreateDirectory(ProjectName);
            ProjectSourceRoot = ProjectDir.Path + Path.DirectorySeparatorChar;
            ProjectObjDir = ProjectDir.CreateDirectory("obj");
            ProjectOutDir = ProjectDir.CreateDirectory("bin").CreateDirectory(Configuration).CreateDirectory(TargetFramework);

            Project = ProjectDir.CreateFile(ProjectFileName).WriteAllText(s_projectSource);
            ProjectDir.CreateFile("TestClass.cs").WriteAllText(s_classSource);
        }

        public static string EnsureTrailingDirectorySeparator(string path)
            => (path.LastOrDefault() == Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

        protected void VerifyValues(
            string customProps, 
            string customTargets,
            string[] targets,
            string[] expressions, 
            string[]? expectedResults = null, 
            string[]? expectedErrors = null, 
            string[]? expectedWarnings = null,
            string? additionalCommandLineArgs = null,
            string buildVerbosity = "minimal",
            Predicate<string>? expectedBuildOutputFilter = null)
        {
            NullableDebug.Assert(targets != null);
            NullableDebug.Assert(expressions != null);
            NullableDebug.Assert(expectedResults == null ^ expectedErrors == null);

            var evaluationResultsFile = Path.Combine(ProjectOutDir.Path, "EvaluationResult.txt");

            EmitTestHelperProps(ProjectObjDir.Path, ProjectFileName, customProps);
            EmitTestHelperTargets(ProjectObjDir.Path, evaluationResultsFile, ProjectFileName, expressions, customTargets);

            var targetsArg = string.Join(";", targets.Concat(new[] { "Test_EvaluateExpressions" }));
            var testBinDirectory = Path.GetDirectoryName(typeof(DotNetSdkTestBase).Assembly.Location);
            var buildLog = Path.Combine(RootDir.Path, $"build{_logIndex++}.binlog");

            bool success = false;
            try
            {
                if (!_projectRestored)
                {
                    var restoreResult = ProcessUtilities.Run(DotNetPath, $@"msbuild ""{Project.Path}"" /t:restore /bl:{Path.Combine(RootDir.Path, "restore.binlog")}",
                        additionalEnvironmentVars: EnvironmentVariables);
                    Assert.True(restoreResult.ExitCode == 0, $"Failed with exit code {restoreResult.ExitCode}: {restoreResult.Output}");

                    Assert.True(File.Exists(Path.Combine(ProjectObjDir.Path, "project.assets.json")));
                    var generatedPropsFilePath = Path.Combine(ProjectObjDir.Path, ProjectFileName + ".nuget.g.props");
                    Assert.True(File.Exists(generatedPropsFilePath));
                    Assert.True(File.Exists(Path.Combine(ProjectObjDir.Path, ProjectFileName + ".nuget.g.targets")));

                    FixupGeneratedPropsFilePath(generatedPropsFilePath);

                    _projectRestored = true;
                }

                var buildResult = ProcessUtilities.Run(DotNetPath, $@"msbuild ""{Project.Path}"" /t:{targetsArg} /p:Configuration={Configuration} /bl:""{buildLog}"" /v:{buildVerbosity} {additionalCommandLineArgs}",
                    additionalEnvironmentVars: EnvironmentVariables);

                string[] getDiagnostics(string[] lines, bool error)
                    => (from line in lines
                        let match = Regex.Match(line, $@"^.*\([0-9]+,[0-9]+\): {(error ? "error" : "warning")} : (.*) \[.*\]$")
                        where match.Success
                        select match.Groups[1].Value).ToArray();

                static bool diagnosticsEqual(string expected, string actual)
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
                    Assert.True(buildResult.ExitCode == 0, $"Build failed with exit code {buildResult.ExitCode}:{Environment.NewLine}{buildResult.Output}{Environment.NewLine}{buildResult.Errors}");

                    var evaluationResult = File.ReadAllLines(evaluationResultsFile).Select(l => (l != EmptyValueMarker) ? l : "");
                    AssertEx.Equal(expectedResults, evaluationResult);
                }
                else
                {
                    Assert.True(buildResult.ExitCode != 0, $"Build succeeded but should have failed:{Environment.NewLine}{buildResult.Output}{Environment.NewLine}{buildResult.Errors}");

                    var actualErrors = getDiagnostics(outputLines, error: true);
                    AssertEx.Equal(expectedErrors, actualErrors, diagnosticsEqual);
                }

                var actualWarnings = getDiagnostics(outputLines, error: false);
                AssertEx.Equal(expectedWarnings ?? Array.Empty<string>(), actualWarnings, diagnosticsEqual);

                if (expectedBuildOutputFilter != null)
                {
                    Assert.Contains(outputLines, expectedBuildOutputFilter);
                }

                success = true;
            }
            finally
            {
                if (!success)
                {
                    try { File.Copy(buildLog, Path.Combine(s_buildInfo.LogDirectory, "test_build_" + Path.GetFileName(RootDir.Path) + ".binlog"), overwrite: true); } catch { }
                }
            }
        }

        // Workaround for https://github.com/NuGet/Home/issues/11455
        private void FixupGeneratedPropsFilePath(string generatedPropsFilePath)
        {
            var content = File.ReadAllText(generatedPropsFilePath, Encoding.UTF8);
            int i = 0;
            content = Regex.Replace(content, "<SourceRoot Include=\"(.*)\" */>",
                match => (i++ == 0) ? $"<SourceRoot Include=\"{NuGetPackageFolders}\" />" : "");
            File.WriteAllText(generatedPropsFilePath, content, Encoding.UTF8);
        }
    }
}
