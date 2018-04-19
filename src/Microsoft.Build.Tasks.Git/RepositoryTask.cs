// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.Git
{
    public abstract class RepositoryTask : Task
    {
        [Required]
        public string LocalRepositoryId { get; set; }

        protected abstract bool Execute(Repository repo);

        public sealed override bool Execute()
        {
            Repository repo;
            try
            {
                repo = new Repository(LocalRepositoryId);
            }
            catch (RepositoryNotFoundException e)
            {
                Log.LogError(e.Message);
                return false;
            }

            if (repo.Info.IsBare)
            {
                Log.LogError($"Bare repositories are not supported: '{LocalRepositoryId}'.");
                return false;
            }

            using (repo)
            {
                try
                {
                    return Execute(repo);
                }
                catch (LibGit2SharpException e)
                {
                    Log.LogError(e.Message);
                    return false;
                }
            }
        }
    }
}
