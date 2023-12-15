// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitConfigTests
    {
        private static IEnumerable<string> Inspect(GitConfig config)
            => config.EnumerateVariables().Select(kvp => $"{kvp.Key}={string.Join("|", kvp.Value)}");

        private static GitConfig LoadFromString(string gitDirectory, string configPath, string configContent)
            => new GitConfig.Reader(gitDirectory, gitDirectory, GitEnvironment.Empty, _ => new StringReader(configContent)).
               LoadFrom(configPath);

        [Fact]
        public void Sections()
        {
            var config = LoadFromString("/", "/config", $@"
a = 1                     ; variable without section
[s1][s2]b = 2             # section without variable followed by another section    
[s3]#[s4]

c =    3 4 "" ""
;xxx
c = "" 5 ""                

d =     
#xxx
");

            AssertEx.SetEqual(new[]
            {
                ".a=1",
                "s2.b=2",
                "s3.c=3 4  | 5 ",
                "s3.d=",
            }, Inspect(config));
        }

        [Fact]
        public void Sections_Errors()
        {
            var e = Assert.Throws<InvalidDataException>(() => LoadFromString("/", "/config", @"
[s]
a = 
1"));
            AssertEx.AreEqual(string.Format(Resources.ErrorParsingConfigLineInFile, 4, "/config", string.Format(Resources.UnexpectedCharacter, "U+0031")), e.Message);
        }

        [Fact]
        public void ConditionalInclude()
        {
            var repoDir = PathUtils.ToPosixPath(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
            var gitDirectory = PathUtils.ToPosixPath(Path.Combine(repoDir, ".git")) + "/";

            TextReader openFile(string path)
            {
                Assert.Equal(gitDirectory, PathUtils.EnsureTrailingSlash(PathUtils.ToPosixPath(Path.GetDirectoryName(path)!)));

                return new StringReader(Path.GetFileName(path) switch
                {
                    "config" => $@"
[x    ""y""] a = 1
[x    ""Y""] a = 2
[x.Y] A = 3

[core]
	symlinks = false
	ignorecase = true

[includeIf ""gitdir:{repoDir}""]            # not included
    path = cfg0                             
                                            
[includeIf ""gitdir:{repoDir}/<>""]         # not included (does not throw)
    path = cfg0                             
                                            
[includeIf ""gitdir:{repoDir}\\.git/""]     # not included (Windows separator)
    path = cfg0                             
                                            
[includeIf ""gitdir:{repoDir}/.git/""]      # not included (file does not exist)
    path = cfg0                                 
                                            
[includeIf ""gitdir:{repoDir}/.git""]       # not included (path doesn't end with slash)
    path = cfg0                         
                                            
[includeIf ""gitdir:{repoDir}?.git/""]      # included
    path = cfg2                             
                                            
[includeIf ""gitdir:{repoDir}[^a].git/""]   # included
    path = cfg3                             
                                            
[includeIf ""gitdir:{repoDir}/""]           # included 
    path = cfg4                             
                                            
[includeIf ""gitdir:{repoDir}/*""]          # included
    path = cfg5                             
                                            
[includeIf ""gitdir:{repoDir}/**""]         # included
    path = cfg6                             
                                            
[includeIf ""gitdir:{repoDir}/**/.git/""]   # included
    path = cfg7

[includeIf ""gitdir/i:{repoDir}/**/.GIT/""] # included
    path = cfg8

[includeIf ""gitdir/i:~/**/.GIT/""]         # included
    path = cfg9

[includeIf ""gitdir:.""]                    # not included
    path = cfg0                            
                                           
[includeIf ""gitdir:./""]                   # included
    path = cfg10
",
                    "cfg1" => "[c]n = cfg1",
                    "cfg2" => "[c]n = cfg2",
                    "cfg3" => "[c]n = cfg3",
                    "cfg4" => "[c]n = cfg4",
                    "cfg5" => "[c]n = cfg5",
                    "cfg6" => "[c]n = cfg6",
                    "cfg7" => "[c]n = cfg7",
                    "cfg8" => "[c]n = cfg8",
                    "cfg9" => "[c]n = cfg9",
                    "cfg10" => "[c]n = cfg10",
                    _ => throw new FileNotFoundException(path)
                });
            }

            var config = new GitConfig.Reader(gitDirectory, gitDirectory, new GitEnvironment(repoDir), openFile).LoadFrom(Path.Combine(gitDirectory, "config"));

            AssertEx.SetEqual(new[]
            {
                "c.n=cfg2|cfg3|cfg4|cfg5|cfg6|cfg7|cfg8|cfg9|cfg10",
                "core.ignorecase=true",
                "core.symlinks=false",
                $"includeif.gitdir/i:{repoDir}/**/.GIT/.path=cfg8",
                $"includeif.gitdir/i:~/**/.GIT/.path=cfg9",
                $"includeif.gitdir:..path=cfg0",
                $"includeif.gitdir:./.path=cfg10",
                $"includeif.gitdir:{repoDir}.path=cfg0",
                $"includeif.gitdir:{repoDir}/**.path=cfg6",
                $"includeif.gitdir:{repoDir}/**/.git/.path=cfg7",
                $"includeif.gitdir:{repoDir}/*.path=cfg5",
                $"includeif.gitdir:{repoDir}/.git.path=cfg0",
                $"includeif.gitdir:{repoDir}/.git/.path=cfg0",
                $"includeif.gitdir:{repoDir}/.path=cfg4",
                $"includeif.gitdir:{repoDir}/<>.path=cfg0",
                $"includeif.gitdir:{repoDir}?.git/.path=cfg2",
                $"includeif.gitdir:{repoDir}[^a].git/.path=cfg3",
                $"includeif.gitdir:{repoDir}\\.git/.path=cfg0",
                "x.y.a=1|3",
                "x.Y.a=2",
            }, Inspect(config));
        }

        [Fact]
        public void ConditionalInclude_HomeNotSupported()
        {
            var gitDirectory = PathUtils.ToPosixPath(Path.Combine(Path.GetTempPath(), ".git")) + "/";

            var config = @"
[includeIf ""gitdir/i:~/**/.GIT/""]
  path = xyz
";

            var reader = new GitConfig.Reader(gitDirectory, gitDirectory, GitEnvironment.Empty, _ => new StringReader(config));

            Assert.Throws<NotSupportedException>(() => reader.LoadFrom(Path.Combine(gitDirectory, "config")));
        }

        [Fact]
        public void IncludeRecursion()
        {
            var gitDirectory = PathUtils.ToPosixPath(Path.Combine(Path.GetTempPath(), ".git")) + "/";

            TextReader openFile(string path)
            {
                Assert.Equal(gitDirectory, PathUtils.EnsureTrailingSlash(PathUtils.ToPosixPath(Path.GetDirectoryName(path)!)));

                return new StringReader(Path.GetFileName(path) switch
                {
                    "config" => @"
[x]
    a = 1

[include]
    path = cfg1
",
                    "cfg1" => @"
[x]
    a = 2

[include]
    path = config
",
                    _ => throw new FileNotFoundException(path)
                });
            }

            var e = Assert.Throws<InvalidDataException>(() => new GitConfig.Reader(gitDirectory, gitDirectory, GitEnvironment.Empty, openFile).LoadFrom(Path.Combine(gitDirectory, "config")));

            AssertEx.AreEqual(string.Format(Resources.ConfigurationFileRecursionExceededMaximumAllowedDepth, 10), e.Message);
        }

        [Theory]
        [InlineData(true, true, "programdata|sys|xdg|home1|common")]
        [InlineData(true, false, "programdata|sys|home2|home1|common")]
        [InlineData(false, true, "sys|xdg|home1|common")]
        public void HierarchicalLoad(bool enableProgramData, bool enableXdg, string expected)
        {
            using var temp = new TempRoot();
            var root = temp.CreateDirectory();

            var gitDir = root.CreateDirectory(".git");

            var commonDir = root.CreateDirectory("common");
            commonDir.CreateFile("config").WriteAllText("[cfg]dir=common");

            var homeDir = root.CreateDirectory("home");
            homeDir.CreateFile(".gitconfig").WriteAllText("[cfg]dir=home1");
            homeDir.CreateDirectory(".config").CreateDirectory("git").CreateFile("config").WriteAllText("[cfg]dir=home2");

            TempDirectory? xdgDir = null;
            if (enableXdg)
            {
                xdgDir = root.CreateDirectory("xdg");
                xdgDir.CreateDirectory("git").CreateFile("config").WriteAllText("[cfg]dir=xdg");
            }

            TempDirectory? programDataDir = null;
            if (enableProgramData)
            {
                programDataDir = root.CreateDirectory("programdata");
                programDataDir.CreateDirectory("git").CreateFile("config").WriteAllText("[cfg]dir=programdata");
            }

            var systemDir = root.CreateDirectory("sys");
            systemDir.CreateFile("gitconfig").WriteAllText("[cfg]dir=sys");

            var gitDirectory = PathUtils.EnsureTrailingSlash(PathUtils.ToPosixPath(gitDir.Path));
            var commonDirectory = PathUtils.EnsureTrailingSlash(PathUtils.ToPosixPath(commonDir.Path));

            var environment = new GitEnvironment(
                homeDirectory: homeDir.Path,
                xdgConfigHomeDirectory: xdgDir?.Path,
                programDataDirectory: programDataDir?.Path,
                systemDirectory : systemDir.Path);

            var reader = new GitConfig.Reader(gitDirectory, commonDirectory, environment, File.OpenText);
            var gitConfig = reader.Load();

            AssertEx.SetEqual(new[]
            {
                "cfg.dir=" + expected
            }, Inspect(gitConfig));
        }

        [Theory]
        [InlineData("[X]", "x", "")]
        [InlineData("[-]", "-", "")]
        [InlineData("[.]", ".", "")]
        [InlineData("[..]", "", ".")]
        [InlineData("[...]", "", "..")]
        [InlineData("[.x]", "", "x")]
        [InlineData("[..x]", "", ".x")]
        [InlineData("[.X]", "", "x")]
        [InlineData("[X.]", "x.", "")]
        [InlineData("[X..]", "x", ".")]
        [InlineData("[X. \"z\"]", "x.", ".z")]
        [InlineData("[X.y]", "x", "y")]
        [InlineData("[X.y.z]", "x", "y.z")]
        [InlineData("[X-]", "x-", "")]
        [InlineData("[-x]", "-x", "")]
        [InlineData("[X-y]", "x-y", "")]
        [InlineData("[X \"y\"]", "x", "y")]
        [InlineData("[X \t\f\v\"y\"]", "x", "y")]
        [InlineData("[X.y \"z\"]", "x", "y.z")]
        [InlineData("[X.Y \"z\"]", "x", "y.z")]
        [InlineData("[X \"/*-\\a\"]", "x", "/*-a")]
        public void ReadSectionHeader(string str, string name, string subsectionName)
        {
            GitConfig.Reader.ReadSectionHeader(
                new GitConfig.LineCountingReader(new StringReader(str), "path"), new StringBuilder(), out var actualName, out var actualSubsectionName);

            Assert.Equal(name, actualName);
            Assert.Equal(subsectionName, actualSubsectionName);
        }

        [Theory]
        [InlineData("[", -1)]
        [InlineData("[x", -1)]
        [InlineData("[x x x]", 'x')]
        [InlineData("[* \"\\", '*')]
        [InlineData("[* \"\\\"]", '*')]
        [InlineData("[* \"*\"]", '*')]
        [InlineData("[x \"y\" ]", ' ')]
        public void ReadSectionHeader_Errors(string str, int unexpectedChar)
        {
            var e = Assert.Throws<InvalidDataException>(() => GitConfig.Reader.ReadSectionHeader(
                new GitConfig.LineCountingReader(new StringReader(str), "path"), new StringBuilder(), out _, out _));

            var message = (unexpectedChar == -1)
                ? Resources.UnexpectedEndOfFile
                : string.Format(Resources.UnexpectedCharacter, $@"U+{unexpectedChar:x4}");

            AssertEx.AreEqual(string.Format(Resources.ErrorParsingConfigLineInFile, 1, "path", message), e.Message);
        }

        [Theory]
        [InlineData("a", "a", "true")]
        [InlineData("A", "a", "true")]
        [InlineData("a\r", "a", "true")]
        [InlineData("a\r\n", "a", "true")]
        [InlineData("a\n", "a", "true")]
        [InlineData("a\n\r", "a", "true")]
        [InlineData("a \n", "a", "true")]
        [InlineData("a# ", "a", "true")]
        [InlineData("a;xxx\n", "a", "true")]
        [InlineData("a #", "a", "true")]
        [InlineData("a=1", "a", "1")]
        [InlineData("a-=1", "a-", "1")]
        [InlineData("a-4=1", "a-4", "1")]
        [InlineData("a-4   =1", "a-4", "1")]
        [InlineData("a=1\nb=1", "a", "1")]
        [InlineData("a=\"1\\\nb=1\"", "a", "1b=1")]
        [InlineData("a=\"1\\nb=1\"", "a", "1\nb=1")]
        [InlineData("name=\"a\"x\"b\"", "name", "axb")]
        [InlineData("name=\"b\"#\"a\"", "name", "b")]
        [InlineData("name=\"b\";\"a\"", "name", "b")]
        [InlineData("name=\\\r\nabc", "name", "abc")]
        [InlineData("name=\"a\\\n bc\"", "name", "a bc")]
        [InlineData("name=a\\\nbc", "name", "abc")]
        [InlineData("name=a\\\n bc", "name", "a bc")]
        [InlineData("name=  3 4 \" \"  ", "name", "3 4  ")]
        [InlineData("name= 1\\t", "name", "1\t")]
        [InlineData("name= 1\\n", "name", "1\n")]
        [InlineData("name= 1\\\\", "name", "1\\")]
        [InlineData("name= 1\\\"", "name", "1\"")]
        [InlineData("name= ", "name", "")]
        [InlineData("name=", "name", "")]
        [InlineData("name=\"a\rb\"", "name", "a\rb")]
        [InlineData("name=\"a\nb\"", "name", "a\nb")]
        [InlineData("name=\"a\r\nb\"", "name", "a\r\nb")]
        public void ReadVariableDeclaration(string str, string name, string value)
        {
            GitConfig.Reader.ReadVariableDeclaration(
                new GitConfig.LineCountingReader(new StringReader(str), "path"), new StringBuilder(), out var actualName, out var actualValue);

            Assert.Equal(name, actualName);
            Assert.Equal(value, actualValue);
        }

        [Theory]
        [InlineData("", -1)]
        [InlineData("*", '*')]
        [InlineData("-=1", '-')]
        [InlineData("_=1", '_')]
        [InlineData("5=1", '5')]
        [InlineData("a_=1", '_')]
        [InlineData("a*=1", '*')]
        [InlineData("name=\\j", '\\')]
        [InlineData("name=\"", -1)]
        [InlineData("name=\"a", -1)]
        [InlineData("name=\"a\n", -1, 2)]
        [InlineData("name=\"a\nb", -1, 2)]
        [InlineData("name=\"a\rb", -1, 2)]
        [InlineData("name=\"a\r\nb", -1, 2)]
        [InlineData("name=\"a\r\rb", -1, 3)]
        public void ReadVariableDeclaration_Errors(string str, int unexpectedChar, int line = 1)
        {
            var e = Assert.Throws<InvalidDataException>(() => GitConfig.Reader.ReadVariableDeclaration(
                new GitConfig.LineCountingReader(new StringReader(str), "path"), new StringBuilder(), out _, out _));

            var message = (unexpectedChar == -1)
                ? Resources.UnexpectedEndOfFile
                : string.Format(Resources.UnexpectedCharacter, $@"U+{unexpectedChar:x4}");

            AssertEx.AreEqual(string.Format(Resources.ErrorParsingConfigLineInFile, line, "path", message), e.Message);
        }

        [Theory]
        [InlineData("0", 0)]
        [InlineData("10", 10)]
        [InlineData("-10", -10)]
        [InlineData("10k", 10 * 1024)]
        [InlineData("-10K", -10 * 1024)]
        [InlineData("10M", 10 * 1024 * 1024)]
        [InlineData("-10m", -10 * 1024 * 1024)]
        [InlineData("10G", 10L * 1024 * 1024 * 1024)]
        [InlineData("-10g", -10L * 1024 * 1024 * 1024)]
        [InlineData("-9223372036854775808", long.MinValue)]
        [InlineData("9223372036854775807", long.MaxValue)]
        public void TryParseInt64Value_Success(string str, long value)
        {
            Assert.True(GitConfig.TryParseInt64Value(str, out var actualValue));
            Assert.Equal(value, actualValue);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("    ")]
        [InlineData("-")]
        [InlineData("k")]
        [InlineData("-9223372036854775809")]
        [InlineData("9223372036854775808")]
        [InlineData("922337203685477580k")]
        [InlineData("922337203685477580G")]
        public void TryParseInt64Value_Error(string str)
        {
            Assert.False(GitConfig.TryParseInt64Value(str, out _));
        }

        [Theory]
        [InlineData("", false)]
        [InlineData("no", false)]
        [InlineData("NO", false)]
        [InlineData("No", false)]
        [InlineData("Off", false)]
        [InlineData("0", false)]
        [InlineData("False", false)]
        [InlineData("1", true)]
        [InlineData("tRue", true)]
        [InlineData("oN", true)]
        [InlineData("yeS", true)]
        public void TryParseBooleanValue_Success(string str, bool value)
        {
            Assert.True(GitConfig.TryParseBooleanValue(str, out var actualValue));
            Assert.Equal(value, actualValue);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("2")]
        [InlineData(" ")]
        [InlineData("x")]
        public void TryParseBooleanValue_Error(string str)
        {
            Assert.False(GitConfig.TryParseBooleanValue(str, out _));
        }
    }
}
