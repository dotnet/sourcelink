// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git
{
    internal static class RepositoryTasks
    {
        private static bool Execute<T>(T task, Action<Repository, T> action)
            where T: RepositoryTask
        {
            var log = task.Log;

            Repository repo;
            try
            {
                repo = new Repository(task.LocalRepositoryId);
            }
            catch (RepositoryNotFoundException e)
            {
                log.LogErrorFromException(e);
                return false;
            }

            if (repo.Info.IsBare)
            {
                log.LogError(Resources.BareRepositoriesNotSupported, task.LocalRepositoryId);
                return false;
            }

            using (repo)
            {
                try
                {
                    action(repo, task);
                }
                catch (LibGit2SharpException e)
                {
                    log.LogErrorFromException(e);
                }
            }

            return !log.HasLoggedErrors;
        }

        public static bool LocateRepository(LocateRepository task)
        {
            task.Id = GitOperations.LocateRepository(task.Directory);

            if (task.Id == null)
            {
                task.Log.LogError(Resources.UnableToLocateRepository, task.Directory);
            }

            return !task.Log.HasLoggedErrors;
        }

        public static bool GetRepositoryUrl(GetRepositoryUrl task) => 
            Execute(task, (repo, t) =>
            {
                t.Url = GitOperations.GetRepositoryUrl(repo, t.RemoteName);
            });

        public static bool GetSourceRevisionId(GetSourceRevisionId task) =>
            Execute(task, (repo, t) =>
            {
                t.RevisionId = GitOperations.GetRevisionId(repo);
            });

        public static bool GetSourceRoots(GetSourceRoots task) =>
            Execute(task, (repo, t) =>
            {
                t.Roots = GitOperations.GetSourceRoots(repo, t.Log.LogWarning, File.Exists);
            });

        public static bool GetUntrackedFiles(GetUntrackedFiles task) =>
            Execute(task, (repo, t) =>
            {
                t.UntrackedFiles = GitOperations.GetUntrackedFiles(repo, t.Files, t.ProjectDirectory, dir => new Repository(dir));
            });
    }
}
