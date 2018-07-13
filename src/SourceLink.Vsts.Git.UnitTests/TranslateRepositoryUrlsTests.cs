// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System.Linq;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Vsts.Git.UnitTests
{
    public class TranslateRepositoryUrlsTests
    {
        [Fact]
        public void Translate()
        {
            var engine = new MockEngine();

            var task = new TranslateRepositoryUrls()
            {
                BuildEngine = engine,
                RepositoryUrl = "ssh://account@vs-ssh.visualstudio.com/collection/project/_ssh/repo",
                IsSingleProvider = true,
                SourceRoots = new[]
                {
                    new MockItem("/1/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@vs-ssh.visualstudio.com:22/DefaultCollection/project/_ssh/repo")),
                    new MockItem("/2/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://account@vs-ssh.visualstudio.com:22/DefaultCollection/project/_ssh/repo")),
                    new MockItem("/3/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@tfs1.com:22/DefaultCollection/project/_ssh/repo")), // no "vs-ssh." prefix
                    new MockItem("/3/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@vs-ssh.tfs1.com:22/DefaultCollection/project/_ssh/repo")),
                    new MockItem("/3/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@vs-ssh.tfs2.com:22/DefaultCollection/project/_ssh/repo")),
                    new MockItem("/3/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@vs-ssh.tfs2.com:22/DefaultCollection/project/ZZZ/repo")), // bad format
                },
                Hosts = new[]
                {
                    new MockItem("tfs1.com")
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual("https://account.visualstudio.com/collection/project/_git/repo", task.TranslatedRepositoryUrl);

            AssertEx.Equal(new[] 
            {
                "https://account.visualstudio.com/DefaultCollection/project/_git/repo",
                "ssh://account@vs-ssh.visualstudio.com:22/DefaultCollection/project/_ssh/repo",
                "ssh://account@tfs1.com:22/DefaultCollection/project/_ssh/repo",
                "https://account.tfs1.com/DefaultCollection/project/_git/repo",
                "ssh://account@vs-ssh.tfs2.com:22/DefaultCollection/project/_ssh/repo",
                "ssh://account@vs-ssh.tfs2.com:22/DefaultCollection/project/ZZZ/repo"
            }, task.TranslatedSourceRoots.Select(r => r.GetMetadata("ScmRepositoryUrl")));

            Assert.True(result);
        }
    }
}
