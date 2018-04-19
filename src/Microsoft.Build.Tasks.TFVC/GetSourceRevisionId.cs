// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Microsoft.Build.Tasks.Tfvc
{
    public sealed class GetSourceRevisionId : RepositoryTask
    {
        [Output]
        public string RevisionId { get; private set; }

        protected override bool Execute(WorkspaceInfo workspaceInfo)
        {
            using (var collection = new TfsTeamProjectCollection(workspaceInfo.ServerUri))
            {
                var vcServer = collection.GetService<VersionControlServer>();
                RevisionId = vcServer.GetLatestChangesetId().ToString();
            }

            return true;
        }
    }
}
