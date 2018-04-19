// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestBranch : Branch
    {
        private readonly string _tipCommitSha;

        public TestBranch(string tipCommitSha)
        {
            _tipCommitSha = tipCommitSha;
        }

        public override Commit Tip => new TestCommit(_tipCommitSha);
    }
}
