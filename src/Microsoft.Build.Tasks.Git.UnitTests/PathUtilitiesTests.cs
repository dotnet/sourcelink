// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class PathUtilitiesTests
    {
        [ConditionalTheory(typeof(WindowsOnly))]
        [InlineData(@"C:\")]
        [InlineData(@"C:\repo")]
        [InlineData(@"C:/repo")]
        [InlineData(@"c:\repo")]
        [InlineData(@"Z:\a\b")]
        [InlineData(@"\\server\share")]
        [InlineData(@"//server/share")]
        [InlineData(@"\\?\C:\repo")]
        public void IsPathFullyQualified_Windows_True(string path)
            => Assert.True(PathUtilities.IsPathFullyQualified(path));

        [ConditionalTheory(typeof(WindowsOnly))]
        [InlineData(@"C:foo")]  // drive-relative: depends on the current directory of drive C
        [InlineData(@"\foo")]   // root-relative: depends on the current drive
        [InlineData(@"/foo")]   // root-relative (alt separator)
        [InlineData(@"C:")]     // drive with no path
        [InlineData(@"1:\foo")] // invalid drive character
        public void IsPathFullyQualified_Windows_False(string path)
            => Assert.False(PathUtilities.IsPathFullyQualified(path));

        [ConditionalTheory(typeof(UnixOnly))]
        [InlineData(@"/")]
        [InlineData(@"/foo")]
        [InlineData(@"/foo/bar")]
        public void IsPathFullyQualified_Unix_True(string path)
            => Assert.True(PathUtilities.IsPathFullyQualified(path));

        [ConditionalTheory(typeof(UnixOnly))]
        [InlineData(@"C:\foo")] // not a special form on Unix
        [InlineData(@"foo")]
        public void IsPathFullyQualified_Unix_False(string path)
            => Assert.False(PathUtilities.IsPathFullyQualified(path));

        [Theory]
        [InlineData("")]
        [InlineData("foo")]
        [InlineData("foo/bar")]
        [InlineData(".")]
        [InlineData("..")]
        public void IsPathFullyQualified_Relative_False(string path)
            => Assert.False(PathUtilities.IsPathFullyQualified(path));

        [Fact]
        public void IsPathFullyQualified_Null_Throws()
            => Assert.Throws<ArgumentNullException>(() => PathUtilities.IsPathFullyQualified(null!));
    }
}
