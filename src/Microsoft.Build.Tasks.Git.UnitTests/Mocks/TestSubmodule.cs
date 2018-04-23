// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestSubmodule : Submodule
    {
        private readonly string _name;
        private readonly string _path;
        private readonly string _url;
        private readonly ObjectId _workDirCommitShaOpt;

        public TestSubmodule(string name, string path, string url, string workDirCommitSha)
        {
            _name = name;
            _path = path;
            _url = url;
            _workDirCommitShaOpt = (workDirCommitSha != null) ? new ObjectId(workDirCommitSha) : null;
        }

        public override string Name => _name;
        public override string Path => _path;
        public override string Url => _url;
        public override ObjectId WorkDirCommitId => _workDirCommitShaOpt;
    }
}
