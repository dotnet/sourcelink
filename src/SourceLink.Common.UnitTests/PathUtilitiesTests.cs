// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using TestUtilities;
using Microsoft.Build.Tasks.SourceControl;
using Xunit;

namespace Microsoft.SourceLink.Common.UnitTests
{
    public class PathUtilitiesTests
    {
        [ConditionalTheory(typeof(WindowsOnly))]
        [InlineData(@"C:\", new[] { "C:" })]
        [InlineData(@"C:\a", new[] { "C:", "a" })]
        [InlineData(@"C:\a\", new[] { "C:", "a" })]
        [InlineData(@"\\server", new[] { @"\\", "server" })]
        [InlineData(@"\\server\share", new[] { @"\\", "server", "share" })]
        public void Split_Windows(string path, string[] expected)
        {
            var actual = PathUtilities.Split(path);
            AssertEx.Equal(expected, actual);
        }

        [ConditionalTheory(typeof(UnixOnly))]
        [InlineData(@"/", new[] { "/" })]
        [InlineData(@"/a", new[] { "/", "a" })]
        [InlineData(@"/a/", new[] { "/", "a" })]
        [InlineData(@"/a/b", new[] { "/", "a", "b" })]
        public void Split_Unix(string path, string[] expected)
        {
            var actual = PathUtilities.Split(path);
            AssertEx.Equal(expected, actual);
        }
    }
}
