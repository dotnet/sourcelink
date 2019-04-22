// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestReference : DirectReference
    {
        private readonly string _sha;

        public TestReference(string sha)
        {
            _sha = sha;
        }

        public override string TargetIdentifier => _sha;
    }
}
