// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestIgnore : Ignore
    {
        private readonly HashSet<string> _ignoredPaths;

        public TestIgnore(IEnumerable<string> ignoredPaths)
        {
            _ignoredPaths = new HashSet<string>(ignoredPaths);
        }

        public override bool IsPathIgnored(string relativePath)
            => _ignoredPaths.Contains(relativePath);
    }
}
