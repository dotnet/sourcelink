// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Microsoft.Build.Tasks.Git.UnitTests;
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
