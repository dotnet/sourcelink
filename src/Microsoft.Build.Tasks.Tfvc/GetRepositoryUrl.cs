// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Microsoft.Build.Tasks.Tfvc
{
    public sealed class GetRepositoryUrl : RepositoryTask
    {
        [Output]
        public string? Url { get; private set; }

        protected override bool Execute(WorkspaceInfo workspaceInfo)
        {
            using var collection = new TfsTeamProjectCollection(workspaceInfo.ServerUri);

            var workspace = workspaceInfo.GetWorkspace(collection);

            // Use the first project:
            var project = workspace.GetTeamProjectForLocalPath(workspaceInfo.MappedPaths.First());

            // Extract GUID from ArtifactUri "vstfs:///Classification/TeamProject/{Guid}":
            var projectId = Path.GetFileName(project.ArtifactUri.GetPath());

            Url = collection.Uri.ToString() + "/" + projectId;

            return true;
        }
    }
}
