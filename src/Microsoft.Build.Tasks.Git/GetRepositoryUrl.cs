// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.Git
{
    public sealed class GetRepositoryUrl : RepositoryTask
    {
        public string RemoteName { get; set; }

        [Output]
        public string Url { get; private set; }

        protected override bool Execute(Repository repo)
        {
            Url = repo.GetRepositoryUrl(RemoteName);
            return true;
        }
    }
}
