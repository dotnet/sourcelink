// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Microsoft.Build.Tasks.Tfvc
{
    public sealed class GetSourceRevisionId : RepositoryTask
    {
        [Output]
        public string? RevisionId { get; private set; }

        protected override bool Execute(WorkspaceInfo workspaceInfo)
        {
            using var collection = new TfsTeamProjectCollection(workspaceInfo.ServerUri);

            var vcServer = collection.GetService<VersionControlServer>();
            RevisionId = vcServer.GetLatestChangesetId().ToString();

            return true;
        }
    }
}
