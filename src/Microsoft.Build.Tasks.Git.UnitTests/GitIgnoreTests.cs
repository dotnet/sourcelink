// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using System.IO;
using System.Linq;
using System.Text;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitIgnoreTests
    {
        [Theory]
        [InlineData("\t", "\t", GitIgnore.PatternFlags.None)]
        [InlineData("\v", "\v", GitIgnore.PatternFlags.None)]
        [InlineData("\f", "\f", GitIgnore.PatternFlags.None)]
        [InlineData("\\ ", " ", GitIgnore.PatternFlags.None)]
        [InlineData(" #", " #", GitIgnore.PatternFlags.None)]
        [InlineData("!x   ", "x", GitIgnore.PatternFlags.Negative)]
        [InlineData("!x/", "x", GitIgnore.PatternFlags.Negative | GitIgnore.PatternFlags.DirectoryPattern)]
        [InlineData("!/x", "x", GitIgnore.PatternFlags.Negative | GitIgnore.PatternFlags.FullPath)]
        [InlineData("x/", "x", GitIgnore.PatternFlags.DirectoryPattern)]
        [InlineData("/x", "x", GitIgnore.PatternFlags.FullPath)]
        [InlineData("//x//", "/x/", GitIgnore.PatternFlags.DirectoryPattern | GitIgnore.PatternFlags.FullPath)]
        [InlineData("\\", "\\", GitIgnore.PatternFlags.None)]
        [InlineData("\\x", "x", GitIgnore.PatternFlags.None)]
        [InlineData("x\\", "x\\", GitIgnore.PatternFlags.None)]
        [InlineData("\\\\", "\\", GitIgnore.PatternFlags.None)]
        [InlineData("\\abc\\xy\\z", "abcxyz", GitIgnore.PatternFlags.None)]
        internal void TryParsePattern(string line, string glob, GitIgnore.PatternFlags flags)
        {
            Assert.True(GitIgnore.TryParsePattern(line, new StringBuilder(), out var actualGlob, out var actualFlags));
            Assert.Equal(glob, actualGlob);
            Assert.Equal(flags, actualFlags);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("  ")]
        [InlineData("#")]
        [InlineData("!")]
        [InlineData("/")]
        [InlineData("//")]
        [InlineData("!/")]
        [InlineData("!//")]
        [InlineData("#" + TestStrings.GB18030)]
        public void TryParsePattern_None(string line)
        {
            Assert.False(GitIgnore.TryParsePattern(line, new StringBuilder(), out _, out _));
        }

        [Fact]
        public void IsIgnored_CaseSensitive()
        {
            using var temp = new TempRoot();

            var rootDir = temp.CreateDirectory();
            var workingDir = rootDir.CreateDirectory("Repo");

            // root
            // A (.gitignore)
            // B
            // C (.gitignore)
            // D1, D2, D3
            var dirA = workingDir.CreateDirectory("A");
            var dirB = dirA.CreateDirectory("B");
            var dirC = dirB.CreateDirectory("C");
            dirC.CreateDirectory("D1");
            dirC.CreateDirectory("D2");
            dirC.CreateDirectory(TestStrings.GB18030);

            dirA.CreateFile(".gitignore").WriteAllText($@"
!z.txt
*.txt
!u.txt
!v.txt
!.git
b/
{TestStrings.GB18030}/
Bar/**/*.xyz
v.txt
");
            dirC.CreateFile(".gitignore").WriteAllText(@"
!a.txt
D2
D1/c.cs
/*.c
");

            var ignore = new GitIgnore(root: null, workingDir.Path, ignoreCase: false);
            var matcher = ignore.CreateMatcher();

            // outside of the working directory:
            Assert.Null(matcher.IsPathIgnored(rootDir.Path));
            Assert.Null(matcher.IsPathIgnored(workingDir.Path.ToUpperInvariant()));
            
            // special case:
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, ".git") + Path.DirectorySeparatorChar));
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, ".git", "config")));

            Assert.False(matcher.IsPathIgnored(workingDir.Path));
            Assert.False(matcher.IsPathIgnored(workingDir.Path + Path.DirectorySeparatorChar));
            Assert.False(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "X")));

            // matches "*.txt"
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", "D1", "b.txt")));

            // matches "!a.txt"
            Assert.False(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", "D1", "a.txt")));

            // matches "*.txt", "!z.txt" is ignored
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", "z.txt")));

            // matches "*.txt", overriden by "!u.txt"
            Assert.False(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", "u.txt")));

            // matches "*.txt", overriden by "!v.txt", which is overriden by "v.txt"
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", "v.txt")));

            // matches directory name "D2"
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", "D2", "E", "a.txt")));

            // does not match "b/" (treated as a file path)
            Assert.False(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", "D1", "b")));

            // matches "b/" (treated as a directory path)
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", "D1", "b") + Path.DirectorySeparatorChar));

            // matches "D3/" (existing directory path)
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", TestStrings.GB18030)));

            // matches "D1/c.cs"
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", "D1", "c.cs")));

            // matches "Bar/**/*.xyz"
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "Bar", "Baz", "Goo", ".xyz")));

            // matches "/*.c"
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", "x.c")));

            // does not match "/*.c"
            Assert.False(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "B", "C", "D1", "x.c")));

            AssertEx.SetEqual(new[]
            {
                "/Repo/.git: True",
                "/Repo/A/B/C/D1/b: True",
                "/Repo/A/B/C/D1: False",
                "/Repo/A/B/C/D2/E: True",
                "/Repo/A/B/C/D2: True",
                $"/Repo/A/B/C/{TestStrings.GB18030}: True",
                "/Repo/A/B/C: False",
                "/Repo/A/B: False",
                "/Repo/A: False",
                "/Repo: False"
            }, matcher.DirectoryIgnoreStateCache.Select(kvp => $"{kvp.Key.Substring(rootDir.Path.Length)}: {kvp.Value}"));
        }

        [Fact]
        public void IsIgnored_IgnoreCase()
        {
            using var temp = new TempRoot();

            var rootDir = temp.CreateDirectory();
            var workingDir = rootDir.CreateDirectory("Repo");

            // root
            // A (.gitignore)
            // diR
            var dirA = workingDir.CreateDirectory("A");
            dirA.CreateDirectory("diR");

            dirA.CreateFile(".gitignore").WriteAllText(@"
*.txt
!a.TXT
dir/
");

            var ignore = new GitIgnore(root: null, PathUtils.ToPosixDirectoryPath(workingDir.Path), ignoreCase: true);
            var matcher = ignore.CreateMatcher();

            // outside of the working directory:
            Assert.Null(matcher.IsPathIgnored(rootDir.Path.ToUpperInvariant()));

            // special case:
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, ".GIT")));

            // matches "*.txt"
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "b.TXT")));

            // matches "!a.TXT"
            Assert.False(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "a.txt")));

            // matches directory name "dir/"
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "DIr", "a.txt")));

            // matches "dir/" (treated as a directory path)
            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "DiR") + Path.DirectorySeparatorChar));

            if (Path.DirectorySeparatorChar == '\\')
            {
                // matches "dir/" (existing directory path, the directory DIR only exists on case-insensitive FS)
                Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "DIR")));
            }

            Assert.True(matcher.IsPathIgnored(Path.Combine(workingDir.Path, "A", "diR")));

            AssertEx.SetEqual(new[]
            {
                "/Repo/A/DIr: True",
                "/Repo/A: False",
                "/Repo: False",
            }, matcher.DirectoryIgnoreStateCache.Select(kvp => $"{kvp.Key.Substring(rootDir.Path.Length)}: {kvp.Value}"));
        }
    }
}
