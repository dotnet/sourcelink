// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestBranch : Branch
    {
        private readonly Reference _reference;

        public TestBranch(string tipCommitSha)
        {
            _reference = new TestReference(tipCommitSha);
        }

        public override Reference Reference => _reference;
    }
}
