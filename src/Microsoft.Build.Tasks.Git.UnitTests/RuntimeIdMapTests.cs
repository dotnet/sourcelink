// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
#if !NET461
using System;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class RuntimeIdMapTests
    {
        [Theory]
        [InlineData("win", "win", "", "")]
        [InlineData("win.7", "win", "7", "")]
        [InlineData("win7-x64", "win7", "", "x64")]
        [InlineData("win8-x64", "win8", "", "x64")]
        [InlineData("win81-x64", "win81", "", "x64")]
        [InlineData("win10-x64", "win10", "", "x64")]
        [InlineData("win11-x64", "win11", "", "x64")]
        [InlineData("alpine.3.7-x86", "alpine", "3.7", "x86")]
        [InlineData("ubuntu.19.01-x64", "ubuntu", "19.01", "x64")]
        [InlineData("centos.7-x64", "centos", "7", "x64")]
        public void ParseRuntimeId(string rid, string expectedOSName, string expectedVersion, string expectedQualifiers)
        {
            RuntimeIdMap.ParseRuntimeId(rid, out var actualOSName, out var actualVersion, out var actualQualifiers);
            Assert.Equal(expectedVersion, string.Join(".", actualVersion));
            Assert.Equal(expectedOSName, actualOSName);
            Assert.Equal(expectedQualifiers, actualQualifiers);
        }

        [Theory]
        [InlineData("1", "1.0.0", 0)]
        [InlineData("1.1", "1.1", 0)]
        [InlineData("1.2", "1.1", 1)]
        [InlineData("1.1", "1.2", -1)]
        [InlineData("1", "10", -1)]
        [InlineData("10", "1", 1)]
        [InlineData("19.01", "19.10", -1)]
        [InlineData("a", "1", 1)]
        [InlineData("a", "A", 1)]
        [InlineData("1", "A", -1)]
        public void CompareVersions(string left, string right, int expected)
        {
            int actual = RuntimeIdMap.CompareVersions(left.Split('.'), right.Split('.'));
            Assert.Equal(expected, Math.Sign(actual));
        }

        [Theory]
        [InlineData("win7-x64", "win-x64")]
        [InlineData("win8-x64", "win-x64")]
        [InlineData("win81-x64", "win-x64")]
        [InlineData("win10-x86", "win-x86")]
        [InlineData("alpine.3.7-x64", "alpine-x64")]
        [InlineData("ubuntu.19.01-x64", "linux-x64")]
        [InlineData("fedora.30-x64", "fedora-x64")]
        [InlineData("centos.7-x64", "rhel-x64")]
        [InlineData("centos.8-x64", "rhel-x64")]
        [InlineData("debian.7-x64", "linux-x64")]
        [InlineData("debian.8-x64", "linux-x64")]
        [InlineData("debian.9-x64", "debian.9-x64")]
        [InlineData("debian.10-x64", "debian.9-x64")]
        [InlineData("osx.10.14-x64", "osx")]
        [InlineData("ubuntu.18.04-x64", "ubuntu.18.04-x64")]
        [InlineData("ubuntu.18.10-x64", "linux-x64")]
        public void GetNativeLibraryDirectoryName(string rid, string expected)
        {
            string actual = RuntimeIdMap.GetNativeLibraryDirectoryName(rid);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("xxx")]
        [InlineData("debian-x86")]
        [InlineData("debian.8-x86")]
        [InlineData("win11-x64")]
        public void GetNativeLibraryDirectoryName_NotSupported(string rid)
        {
            Assert.Throws<PlatformNotSupportedException>(() => RuntimeIdMap.GetNativeLibraryDirectoryName(rid));
        }
    }
}
#endif