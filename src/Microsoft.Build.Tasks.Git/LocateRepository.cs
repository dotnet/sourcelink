// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.Git
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
                Log.LogError($"Unable to locate repository containing directory '{Directory}'.");
                return false;
            }

            return true;
        }
    }
}
