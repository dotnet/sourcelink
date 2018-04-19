// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Common;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Microsoft.Build.Tasks.Tfvc
{
    public sealed class GetUntrackedSourceFiles : RepositoryTask
    {
        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        [Output]
        public ITaskItem[] UntrackedFiles { get; set; }

        protected override bool Execute(WorkspaceInfo workspaceInfo)
        {
            using (var collection = new TfsTeamProjectCollection(workspaceInfo.ServerUri))
            {
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
            }

            return true;
        }
    }
}
