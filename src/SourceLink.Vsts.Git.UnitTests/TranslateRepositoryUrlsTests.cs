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
                RepositoryUrl = "ssh://account@vs-ssh.visualstudio.com/project/team/_ssh/repo",
                IsSingleProvider = true,
                SourceRoots = new[]
                {
                    new MockItem("/1/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@vs-ssh.visualstudio.com:22/project/team/_ssh/repo")),    // ok
                    new MockItem("/2/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://test@vs-ssh.visualstudio.com:22/project/_ssh/repo")),      // ok
                    new MockItem("/3/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://user@vs-ssh.visualstudio.com:22/v3/account/project/team/repo")), // ok
                    new MockItem("/4/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@ssh.contoso.com:22/project/team/_ssh/repo")),            // ok
                    new MockItem("/5/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://user@ssh.contoso.com/v3/account/project/team/repo")),            // ok
                    new MockItem("/6/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://account@vs-ssh.visualstudio.com:22/project/team/_ssh/repo")),   // different source control
                    new MockItem("/7/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@contoso.com:22/project/team/_ssh/repo")),                // no "vs-ssh." prefix
                    new MockItem("/8/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@vs-ssh.contoso.com:22/project/team/_ssh/repo")),         // known host, but not visualstudio.com
                    new MockItem("/9/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@vs-ssh.contoso2.com:22/project/team/_ssh/repo")),        // unknown host
                    new MockItem("/A/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@vs-ssh.contoso.com:22/project/team/ZZZ/repo")),          // bad format
                },
                Hosts = new[]
                {
                    new MockItem("contoso.com")
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual("https://account.visualstudio.com/project/team/_git/repo", task.TranslatedRepositoryUrl);

            AssertEx.Equal(new[] 
            {
                "https://account.visualstudio.com/project/team/_git/repo",
                "https://test.visualstudio.com/project/_git/repo",
                "https://account.visualstudio.com/project/team/_git/repo",
                "https://contoso.com/account/project/team/_git/repo",
                "https://contoso.com/account/project/team/_git/repo",
                "ssh://account@vs-ssh.visualstudio.com:22/project/team/_ssh/repo",
                "ssh://account@contoso.com:22/project/team/_ssh/repo",
                "ssh://account@vs-ssh.contoso.com:22/project/team/_ssh/repo",
                "ssh://account@vs-ssh.contoso2.com:22/project/team/_ssh/repo",
                "ssh://account@vs-ssh.contoso.com:22/project/team/ZZZ/repo"
            }, task.TranslatedSourceRoots.Select(r => r.GetMetadata("ScmRepositoryUrl")));

            Assert.True(result);
        }
    }
}
