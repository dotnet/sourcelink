﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using TestUtilities;
using Microsoft.Build.Tasks.SourceControl;
using Xunit;

namespace Microsoft.SourceLink.Common.UnitTests
{
    public class UriUtilitiesTests
    {
        [Theory]
        [InlineData("/", "/", true)]
        [InlineData("/a/b", "/", true)]
        [InlineData("/a/b", "/a", true)]
        [InlineData("/a/b", "/a/", true)]
        [InlineData("/a/b", "/a/b", true)]
        [InlineData("/a/b", "/a/b/", true)]
        [InlineData("/a/b/", "/", true)]
        [InlineData("/a/b/", "/a", true)]
        [InlineData("/a/b/", "/a/", true)]
        [InlineData("/a/b/", "/a/b", true)]
        [InlineData("/a/b/", "/a/b/", true)]
        [InlineData("/a/B/", "/a/b/", false)]
        [InlineData("/a/", "/a/b/", false)]
        [InlineData("/", "/a/b/", false)]
        public void UrlStartsWith(string url, string prefix, bool expected)
        {
            Assert.Equal(expected, UriUtilities.UrlStartsWith(url, prefix));
        }

        [Theory]
        [InlineData("", new string[0])]
        [InlineData("/", new string[0])]
        [InlineData("//", null)]
        [InlineData("///", null)]
        [InlineData("////", null)]
        [InlineData("/a", new[] { "a" })]
        [InlineData("a/", new[] { "a" })]
        [InlineData("/a/", new[] { "a" })]
        [InlineData("/a//b/", null)]
        [InlineData("/a/b//", null)]
        public void TrySplitRelativeUrl(string url, string[]? parts)
        {
            if (!UriUtilities.TrySplitRelativeUrl(url, out string[]? actualParts))
            {
                actualParts = null;
            }

            AssertEx.Equal(parts, actualParts);
        }
    }
}
