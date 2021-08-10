// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Microsoft.Build.Tasks.Tfvc
{
    public abstract class RepositoryTask : Task
    {
        [Required, NotNull]
        public string? WorkspaceDirectory { get; set; }

        protected abstract bool Execute(WorkspaceInfo workspaceInfo);

        public sealed override bool Execute()
        {
            var workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(WorkspaceDirectory);
            if (workspaceInfo == null)
            {
                Log.LogError($"Invalid repository id: {WorkspaceDirectory}");
                return false;
            }

            return Execute(workspaceInfo);
        }
    }
}
