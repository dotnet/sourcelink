// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Microsoft.Build.Tasks.Tfvc
{
    public sealed class GetUntrackedSourceFiles : RepositoryTask
    {
        [Required, NotNull]
        public ITaskItem[]? SourceFiles { get; set; }

        [Output]
        public ITaskItem[]? UntrackedFiles { get; set; }

        protected override bool Execute(WorkspaceInfo workspaceInfo)
        {
            using var collection = new TfsTeamProjectCollection(workspaceInfo.ServerUri);

            var vcServer = collection.GetService<VersionControlServer>();
            var workspace = vcServer.GetWorkspace(workspaceInfo);
            var evaluator = new LocalItemExclusionEvaluator(workspace, startLocalItem: ""); // TODO?

            var result = new List<ITaskItem>();
            foreach (var item in SourceFiles)
            {
                if (FileSpec.IsSubItem(item.ItemSpec, evaluator.StartLocalItem) && 
                    evaluator.IsExcluded(item.ItemSpec, isFolder: false))
                {
                    result.Add(item);
                }
            }

            UntrackedFiles = result.ToArray();

            return true;
        }
    }
}
