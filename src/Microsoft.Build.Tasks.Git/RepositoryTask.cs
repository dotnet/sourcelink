// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.Git
{
    public abstract class RepositoryTask : Task
    {
#if NET461
        static RepositoryTask() => AssemblyResolver.Initialize();
#endif
        private static readonly string s_cacheKey = "SourceLinkLocateRepository-3AE29AB7-AE6B-48BA-9851-98A15ED51C94";

        [Required]
        public string Directory { get; set; }

        public sealed override bool Execute()
        {
#if NET461
            bool logAssemblyLoadingErrors()
            {
                foreach (var message in AssemblyResolver.GetLog())
                {
                    Log.LogMessage(message);
                }
                return false;
            }

            try
            {
                ExecuteImpl();
            }
            catch when (logAssemblyLoadingErrors())
            {
            }
#else
            ExecuteImpl();
#endif

            return !Log.HasLoggedErrors;
        }

        private protected abstract void Execute(GitRepository repository);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ExecuteImpl()
        {
            var repository = GetOrCreateRepositoryInstance();
            if (repository == null)
            {
                // error has already been reported
                return;
            }

            try
            {
                Execute(repository);
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is NotSupportedException)
            {
                Log.LogError(Resources.ErrorReadingGitRepositoryInformation, e.Message);
            }
        }

        private GitRepository GetOrCreateRepositoryInstance()
        {
            var cachedEntry = (StrongBox<GitRepository>)BuildEngine4.GetRegisteredTaskObject(s_cacheKey, RegisteredTaskObjectLifetime.Build);
            if (cachedEntry != null)
            {
                Log.LogMessage(MessageImportance.Low, $"SourceLink: Reusing cached git repository information.");
                return cachedEntry.Value;
            }

            GitRepository repository;
            try
            {
                // TODO: configure environment
                repository = GitRepository.OpenRepository(Directory, GitEnvironment.CreateFromProcessEnvironment());
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is NotSupportedException)
            {
                Log.LogError(Resources.ErrorReadingGitRepositoryInformation, e.Message);
                repository = null;
            }

            if (repository?.WorkingDirectory == null)
            {
                Log.LogWarning(Resources.UnableToLocateRepository, Directory);
                repository = null;
            }

            BuildEngine4.RegisterTaskObject(
                s_cacheKey, 
                new StrongBox<GitRepository>(repository),
                RegisteredTaskObjectLifetime.Build,
                allowEarlyCollection: true);

            return repository;
        }
    }
}
