// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GlobTests
    {
        [Theory]
        [InlineData("?", "?")]
        [InlineData("*", "")]
        [InlineData("*", "a")]
        [InlineData("*", "abc")]
        [InlineData("a*", "abc")]
        [InlineData("a**", "abc")]
        [InlineData("a*************************", "abc")]
        [InlineData(".", ".")]
        [InlineData("./", "./")]
        [InlineData(".x", ".x")]
        [InlineData("?x", ".x")]
        [InlineData("*x", ".x")]
        [InlineData("a/**", "a/")]
        [InlineData("a/**", "a/b")]
        [InlineData("**/b", "b")]
        [InlineData("**/a*", "abc")]
        [InlineData("**/b", "a/b")]
        [InlineData("a/**/b", "a/b")]
        [InlineData("a/**/b", "a/x/yb/b")]
        [InlineData("A/**/B/**/C/*.D", "A/z/u/B/q/r/C/z.D")]
        [InlineData("A/**/B*C*D/**/E", "A/u/v/BaaCaaX/u/BoCoD/u/E")]
        [InlineData("a/**/*.x", "a/b/c/d.x")]
        [InlineData("a*b*c*d", "axxbyyczzd")]
        [InlineData("a*?b", "abb")]
        [InlineData("a*bcd", "axbbcybcd")]
        [InlineData("a*bcd*", "axbbcdybcd")]
        [InlineData("*/b", "/b")]
        [InlineData(@"\", @"\")]
        [InlineData(@"\/", @"/")]
        [InlineData(@"\t", @"t")]
        [InlineData(@"\?", @"?")]
        [InlineData(@"\\", @"\")]
        [InlineData("*", @"\")]
        [InlineData("?", @"\")]
        [InlineData("[a-]]", "a]")]
        [InlineData("[a-]]", "-]")]
        public void Matching(string pattern, string path)
        {
            Assert.True(Glob.IsMatch(pattern, path, ignoreCase: false, matchWildCardWithDirectorySeparator: true));
            Assert.True(Glob.IsMatch(pattern, path, ignoreCase: false, matchWildCardWithDirectorySeparator: false));
        }

        [Theory]
        [InlineData("?", "/")]
        [InlineData("*", "/")]
        [InlineData("*", "a/")]
        [InlineData("[--0]", "/")]
        [InlineData("[/]", "/")]
        [InlineData("a*?b", "a/b")]
        [InlineData("a*?b", "ab/b")]
        public void Matching_WildCardMatchesDirectorySeparator(string pattern, string path)
        {
            Assert.True(Glob.IsMatch(pattern, path, ignoreCase: false, matchWildCardWithDirectorySeparator: true));
        }

        [Theory]
        [InlineData("?", "")]
        [InlineData("?", "/")]
        [InlineData("*", "/")]
        [InlineData("*.txt", "")]
        [InlineData("a/**", "a")]
        [InlineData("a/**/*", "a")]
        [InlineData("*", "a/")]
        [InlineData("a*b*c*d", "axxbyyczz")]
        [InlineData("a*d", "abc/de")]
        [InlineData("***/b", "b")]
        [InlineData("a*?b", "a/b")]
        [InlineData("a*?b", "ab/b")]
        [InlineData("a*bcd", "axbbcybcdz")]
        [InlineData("a*bcd", "axbbcdybcd")]
        [InlineData("[/]", "/")]
        [InlineData("[--0]", "/")]
        [InlineData("[", "[")]
        [InlineData("[!", "[!")]
        [InlineData("[a", "[a")]
        [InlineData("[a-", "[a-")]
        [InlineData("[a-]]", "]]")]
        public void NonMatching(string pattern, string path)
        {
            Assert.False(Glob.IsMatch(pattern, path, ignoreCase: false, matchWildCardWithDirectorySeparator: false));
        }

        [Theory]
        [InlineData("[][!]", new[] { '[', ']', '!' })]
        [InlineData("[A-Ca-b0-1]", new[] { 'A', 'B', 'C', 'a', 'b', '0', '1' })]
        [InlineData("[--0]", new[] { '-', '.', '0' }, new[] { '-', '.', '0', '/' })]         // range contains '-', '.', '/', '0', but '/' should not match
        [InlineData("[]-]", new[] { ']', '-' })]
        [InlineData("[a-]", new[] { 'a', '-' })]  
        [InlineData(@"[\]", new[] { '\\' })]
        [InlineData(@"[[?*\]", new[] { '[', '?', '*', '\\' })]
        [InlineData("[b-a]", new[] { 'b' })]
        [InlineData("[!]", new char[0])]
        [InlineData("[^]", new char[0])]
        [InlineData("[]", new char[0])]
        [InlineData("[a-]]", new char[0])]
        public void MatchingRange(string pattern, char[] matchingChars, char[]? matchingCharsWildCardMatchesSeparator = null)
        {
            for (int i = 0; i < 255; i++)
            {
                var c = (char)i;
                bool shouldMatch = Array.IndexOf(matchingChars, c) >= 0;

                Assert.True(shouldMatch == Glob.IsMatch(pattern, c.ToString(), ignoreCase: false, matchWildCardWithDirectorySeparator: false), 
                    $"character: '{(i != 0 ? c.ToString() : "\\0")}' (0x{i:X2})");

                if (matchingCharsWildCardMatchesSeparator != null)
                {
                    shouldMatch = Array.IndexOf(matchingCharsWildCardMatchesSeparator, c) >= 0;
                }

                Assert.True(shouldMatch == Glob.IsMatch(pattern, c.ToString(), ignoreCase: false, matchWildCardWithDirectorySeparator: true),
                    $"character: '{(i != 0 ? c.ToString() : "\\0")}' (0x{i:X2})");
            }
        }

        [Theory]
        [InlineData("[^/]", new[] { '/' })]
        [InlineData("[^--0]", new[] { '-', '.', '/', '0' })]  // range contains '-', '.', '/', '0'
        public void NonMatchingRange(string pattern, char[] nonMatchingChars)
        {
            for (int i = 0; i < 255; i++)
            {
                var c = (char)i;
                bool shouldMatch = Array.IndexOf(nonMatchingChars, c) < 0;
                Assert.True(shouldMatch == Glob.IsMatch(pattern, c.ToString(), ignoreCase: false, matchWildCardWithDirectorySeparator: false),
                    $"character: '{(i != 0 ? c.ToString() : "\\0")}' (0x{i:X2})");

                Assert.True(shouldMatch == Glob.IsMatch(pattern, c.ToString(), ignoreCase: false, matchWildCardWithDirectorySeparator: true),
                    $"character: '{(i != 0 ? c.ToString() : "\\0")}' (0x{i:X2})");
            }
        }

        [Theory]
        [InlineData("[!]a-]", new[] { ']', 'a', '-', '/' })]
        public void NonMatchingRange_WildCardDoesNotMatchDirectorySeparator(string pattern, char[] nonMatchingChars)
        {
            for (int i = 0; i < 255; i++)
            {
                var c = (char)i;
                bool shouldMatch = Array.IndexOf(nonMatchingChars, c) < 0;
                Assert.True(shouldMatch == Glob.IsMatch(pattern, c.ToString(), ignoreCase: false, matchWildCardWithDirectorySeparator: false),
                    $"character: '{(i != 0 ? c.ToString() : "\\0")}' (0x{i:X2})");
            }
        }

        [Theory]
        [InlineData("[!]a-]", new[] { ']', 'a', '-' })]
        public void NonMatchingRange_WildCardMatchesDirectorySeparator(string pattern, char[] nonMatchingChars)
        {
            for (int i = 0; i < 255; i++)
            {
                var c = (char)i;
                bool shouldMatch = Array.IndexOf(nonMatchingChars, c) < 0;
                Assert.True(shouldMatch == Glob.IsMatch(pattern, c.ToString(), ignoreCase: false, matchWildCardWithDirectorySeparator: true),
                    $"character: '{(i != 0 ? c.ToString() : "\\0")}' (0x{i:X2})");
            }
        }

        [Theory]
        [InlineData("[a-b0-1]", new[] { 'A', 'B', 'a', 'b', '0', '1' })]
        [InlineData("[a-]", new[] { 'a', 'A', '-' })]
        [InlineData("[b-a]", new[] { 'b', 'B' })]
        public void MatchingRangeIgnoreCase(string pattern, char[] matchingChars)
        {
            for (int i = 0; i < 255; i++)
            {
                var c = (char)i;
                bool shouldMatch = Array.IndexOf(matchingChars, c) >= 0;
                Assert.True(shouldMatch == Glob.IsMatch(pattern, c.ToString(), ignoreCase: true, matchWildCardWithDirectorySeparator: false),
                    $"character: '{(i != 0 ? c.ToString() : "\\0")}' (0x{i:X2})");

                Assert.True(shouldMatch == Glob.IsMatch(pattern, c.ToString(), ignoreCase: true, matchWildCardWithDirectorySeparator: true),
                    $"character: '{(i != 0 ? c.ToString() : "\\0")}' (0x{i:X2})");
            }
        }
    }
}
