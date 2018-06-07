// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using LibGit2Sharp;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class GitUtilities
    {
        private static readonly Signature s_signature = new Signature("test", "test@test.com", DateTimeOffset.Now);

        public static Repository CreateGitRepositoryWithSingleCommit(string directory, string[] commitFileNames, string originUrl)
        {
            var repository = new Repository(Repository.Init(workingDirectoryPath: directory, gitDirectoryPath: Path.Combine(directory, ".git")));
            repository.Network.Remotes.Add("origin", originUrl);

            foreach (var fileName in commitFileNames)
            {
                repository.Index.Add(fileName);
            }

            repository.Index.Write();
            repository.Commit("First commit", s_signature, s_signature);

            return repository;
        }
    }
}
