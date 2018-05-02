﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Microsoft.Build.Tasks.Git.UnitTests;
using System.Linq;
using Xunit;

namespace Microsoft.Build.Tasks.SourceControl.UnitTests
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

#if todo
        [Fact]
        public void GetExternal1()
        {
            var e = GetExternalFiles.GetExternal(files: new[] { @"C:\a\x.cs", @"C:\a/b\x.cs", @"C:\a\c\\x.cs" }, directories: new[] { @"C:\A" }, s => s).ToArray();
            Assert.Empty(e);
        }

        [Fact]
        public void GetExternal2()
        {
            var e = GetExternalFiles.GetExternal(files: new[] { @"C:\a.cs" }, directories: new[] { @"C:\a" }, s => s).ToArray();
            Assert.Single(e);
            Assert.Equal(@"C:\a.cs", e[0]);
        }

        [Fact]
        public void GetExternal3()
        {
            var e = GetExternalFiles.GetExternal(files: new[] { @"C:\b\x.cs" }, directories: new[] { @"C:\a" }, s => s).ToArray();
            Assert.Single(e);
            Assert.Equal(@"C:\b\x.cs", e[0]);
        }

        [Fact]
        public void GetExternal4()
        {
            var e = GetExternalFiles.GetExternal(
                files: new[] { @"C:\a\y.cs", @"C:\b\x.cs", @"\\z\\q\\a.cs", @"D:\x\x.cs" }, 
                directories: new[] { @"C:\a", @"\\u\v", @"C:\b", @"D:\x" }, s => s).ToArray();

            Assert.Single(e);
            Assert.Equal(@"\\z\\q\\a.cs", e[0]);
        }

        [Fact]
        public void GetExternal5()
        {
            var e = GetExternalFiles.GetExternal(files: new[] { @"C:\a\y.cs", @"C:\b\x.cs", @"\\z\q\a.cs", @"C:\g.cs" }, directories: new[] { @"\\z\q" }, s => s).ToArray();
            Assert.Equal(3, e.Length);
            Assert.Equal(@"C:\a\y.cs", e[0]);
            Assert.Equal(@"C:\b\x.cs", e[1]);
            Assert.Equal(@"C:\g.cs", e[2]);
        }

        [Fact]
        public void GetExternal6()
        {
            var e = GetExternalFiles.GetExternal(files: new[] { @"C:\a\y.cs", @"C:\b\x.cs", @"\\z\q\a.cs", @"C:\g.cs" }, directories: new[] { @"C:\" }, s => s).ToArray();
            Assert.Equal(@"\\z\q\a.cs", e.Single());
        }

        [Fact]
        public void GetExternal7()
        {
            var e = GetExternalFiles.GetExternal(
                files: new[] { @"/x/y/z" },
                directories: new[] { @"/" }, s => s).ToArray();

            Assert.Empty(e);
        }

        [Fact]
        public void GetExternal8()
        {
            var e = GetExternalFiles.GetExternal(
                files: new[] { @"C:\a\y.cs", @"C:\b\x.cs", @"\\z\q\a.cs", @"C:\g.cs", @"/x/y/z" },
                directories: new[] { @"C:\", @"\\z\q", @"/" }, s => s).ToArray();

            Assert.Empty(e);
        }
#endif
    }
}
