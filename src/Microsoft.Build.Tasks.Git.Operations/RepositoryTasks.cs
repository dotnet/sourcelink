// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git
{
    internal static class RepositoryTasks
    {
        private static bool Execute<T>(T task, Action<IRepository, T> action)
            where T: RepositoryTask
        {
            var log = task.Log;

            // Unable to determine repository root, warning has already been reported.
            if (string.IsNullOrEmpty(task.Root))
            {
                return true;
            }

            IRepository repo;
            try
            {
                repo = GitOperations.CreateRepository(task.Root);
            }
            catch (RepositoryNotFoundException e)
            {
                log.LogErrorFromException(e);
                return false;
            }

            if (repo.Info.IsBare)
            {
                log.LogWarning(Resources.BareRepositoriesNotSupported, task.Root);
                return true;
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
            try
            {
                task.Id = GitOperations.LocateRepository(task.Directory);
            }
            catch (Exception e)
            {
#if NET461
                foreach (var message in TaskImplementation.GetLog())
                {
                    task.Log.LogMessage(message);
                }
#endif
                task.Log.LogWarningFromException(e, showStackTrace: true);

                return true;
            }

            if (task.Id == null)
            {
                task.Log.LogWarning(Resources.UnableToLocateRepository, task.Directory);
            }

            return !task.Log.HasLoggedErrors;
        }

        public static bool GetRepositoryUrl(GetRepositoryUrl task) => 
            Execute(task, (repo, t) =>
            {
                t.Url = GitOperations.GetRepositoryUrl(repo, t.Log.LogWarning, t.RemoteName);
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
