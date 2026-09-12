// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System.Linq;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.AzureRepos.Git.UnitTests
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
                RepositoryUrl = "ssh://vs-ssh.visualstudio.com/v3/account/project/team/repo",
                IsSingleProvider = true,
                SourceRoots = new[]
                {
                    new MockItem("/1/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://vs-ssh.visualstudio.com:22/v3/account/project/team/repo")),      // ok
                    new MockItem("/2/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://user@vs-ssh.visualstudio.com:22/v3/test/project/repo")),      // ok
                    new MockItem("/3/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://user@vs-ssh.visualstudio.com:22/v3/account/project/team/repo")), // ok
                    new MockItem("/4/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://vs-ssh.visualstudio.com/v3/account/project/repo")),              // ok
                    new MockItem("/5/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://ssh.contoso.com:22/v3/account/project/team/repo")),              // ok
                    new MockItem("/6/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://user@ssh.contoso.com/v3/account/project/team/repo")),            // ok
                    new MockItem("/7/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://vs-ssh.visualstudio.com:22/v3/account/project/team/repo")),     // different source control
                    new MockItem("/8/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://contoso.com:22/v3/account/project/team/repo")),                  // no "vs-ssh." prefix
                    new MockItem("/9/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://vs-ssh.contoso.com:22/v3/account/project/team/repo")),           // known host, but not visualstudio.com
                    new MockItem("/A/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://vs-ssh.contoso2.com:22/v3/account/project/team/repo")),          // unknown host
                    new MockItem("/B/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://vs-ssh.contoso.com:22/v3/account/project/team/ZZZ/repo")),       // bad format
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
                "https://account.visualstudio.com/project/_git/repo",
                "https://contoso.com/account/project/team/_git/repo",
                "https://contoso.com/account/project/team/_git/repo",
                "ssh://vs-ssh.visualstudio.com:22/v3/account/project/team/repo",
                "ssh://contoso.com:22/v3/account/project/team/repo",
                "ssh://vs-ssh.contoso.com:22/v3/account/project/team/repo",
                "ssh://vs-ssh.contoso2.com:22/v3/account/project/team/repo",
                "ssh://vs-ssh.contoso.com:22/v3/account/project/team/ZZZ/repo"
            }, task.TranslatedSourceRoots?.Select(r => r.GetMetadata("ScmRepositoryUrl")));

            Assert.True(result);
        }
    }
}
