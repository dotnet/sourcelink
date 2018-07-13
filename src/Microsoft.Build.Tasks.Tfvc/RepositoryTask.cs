﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Microsoft.Build.Tasks.Tfvc
{
    public abstract class RepositoryTask : Task
    {
        [Required]
        public string WorkspaceDirectory { get; set; }

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
