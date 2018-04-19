// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestRepositoryInformation : RepositoryInformation
    {
        private readonly string _workingDirectory;

        public TestRepositoryInformation(string workingDirectory)
        {
            _workingDirectory = workingDirectory;
        }

        public override string WorkingDirectory => _workingDirectory;
        public override bool IsBare => false;
    }
}
