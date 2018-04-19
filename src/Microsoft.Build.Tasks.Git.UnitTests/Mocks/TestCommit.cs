// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestCommit : Commit
    {
        private readonly string _sha;

        public TestCommit(string sha)
        {
            _sha = sha;
        }

        public override string Sha => _sha;
    }
}
