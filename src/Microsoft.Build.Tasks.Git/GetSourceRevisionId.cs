// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.Git
{
    public sealed class GetSourceRevisionId : RepositoryTask
    {
        [Output]
        public string RevisionId { get; private set; }

        protected override bool Execute(Repository repo)
        {
            RevisionId = repo.GetRevisionId();
            return true;
        }
    }
}
