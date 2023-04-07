// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.IO;
using LibGit2Sharp;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class GitUtilities
    {
        private static readonly Signature s_signature = new Signature("test", "test@test.com", DateTimeOffset.Now);

        public static Repository CreateGitRepository(string directory, string[]? commitFileNames, string? originUrl)
        {
            var repository = new Repository(Repository.Init(workingDirectoryPath: directory, gitDirectoryPath: Path.Combine(directory, ".git")));

            if (originUrl != null)
            {
                repository.Network.Remotes.Add("origin", originUrl);
            }

            if (commitFileNames != null)
            {
                foreach (var fileName in commitFileNames)
                {
                    repository.Index.Add(fileName);
                }

                repository.Index.Write();
                repository.Commit("First commit", s_signature, s_signature);
            }

            return repository;
        }
    }
}
