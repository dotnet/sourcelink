// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitOperationsTests
    {
        [Fact]
        public void GetSourceRoots()
        {
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var repo = new TestRepository(
                workingDir: root,
                headTipCommitSha: "8398cdcd9043724b9bef1efda8a703dfaa336c0f",
                remotes: new[] { new TestRemote("origin", "http://github.com/myorg/myproj.git") },
                submodules: new[] { new TestSubmodule("src/sub", "http://github.com/sub", "6acefc85cad85b37eaf4b81d83e1eaa4eb399e64") },
                ignoredPaths: new[] { "ignoreme" });
        }
    }
}
