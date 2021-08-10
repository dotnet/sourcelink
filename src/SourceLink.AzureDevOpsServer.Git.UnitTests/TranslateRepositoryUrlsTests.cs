// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System.Linq;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.AzureDevOpsServer.Git.UnitTests
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
                RepositoryUrl = "ssh://account@mytfs.com/a/b/project/team/_ssh/repo",
                IsSingleProvider = true,
                SourceRoots = new[]
                {
                    new MockItem("/1/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@mytfs.com:22/tfs/project/team/_ssh/repo")),    // ok
                    new MockItem("/2/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@contoso.com/tfs/project/_ssh/repo")),          // ok
                    new MockItem("/3/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@mytfs.com/v3/account/project/team/repo")),     // v3 not supported for on-prem
                    new MockItem("/4/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://account@mytfs.com/tfs/project/team/_ssh/repo")),      // different source control
                    new MockItem("/5/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@contoso2.com:22/tfs/project/team/_ssh/repo")), // unknown host
                    new MockItem("/6/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@contoso.com:22/tfs/project/team/ZZZ/repo")),   // bad format
                },
                Hosts = new[]
                {
                    new MockItem("contoso.com")
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            // SSH translation does not need virtual directory
            AssertEx.AreEqual("https://mytfs.com/a/b/project/team/_git/repo", task.TranslatedRepositoryUrl);

            AssertEx.Equal(new[]
            {
                "https://mytfs.com/tfs/project/team/_git/repo",
                "https://contoso.com/tfs/project/_git/repo",
                "ssh://account@mytfs.com/v3/account/project/team/repo",
                "ssh://account@mytfs.com/tfs/project/team/_ssh/repo",
                "ssh://account@contoso2.com:22/tfs/project/team/_ssh/repo",
                "ssh://account@contoso.com:22/tfs/project/team/ZZZ/repo"
            }, task.TranslatedSourceRoots?.Select(r => r.GetMetadata("ScmRepositoryUrl")));

            Assert.True(result);
        }
    }
}
