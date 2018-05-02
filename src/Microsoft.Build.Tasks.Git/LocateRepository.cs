// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Git;
using Microsoft.Build.Utilities;

namespace SourceControlBuildTasks
{
    public class LocateRepository : Task
    {
        [Required]
        public string Directory { get; set; }

        [Output]
        public string Id { get; set; }

        public override bool Execute()
        {
            Id = GitOperations.LocateRepository(Directory);

            if (Id == null)
            {
                Log.LogError(Resources.UnableToLocateRepository, Directory);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
