// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System.Linq;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Bitbucket.Git.UnitTests
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
                RepositoryUrl = "ssh://bitbucket.org/a/b",
                IsSingleProvider = true,
                SourceRoots = new[]
                {
                    new MockItem("/1/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://bitbucket.org:22/a/b")),
                    new MockItem("/2/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://bitbucket1.org/a/b")),
                    new MockItem("/2/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://bitbucket1.org/a/b")),
                    new MockItem("/2/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://bitbucket2.org/a/b")),
                },
                Hosts = new[]
                {
                    new MockItem("bitbucket1.org")
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual("https://bitbucket.org/a/b", task.TranslatedRepositoryUrl);

            AssertEx.Equal(new[] 
            {
                "https://bitbucket.org/a/b",
                "ssh://bitbucket1.org/a/b",
                "https://bitbucket1.org/a/b",
                "ssh://bitbucket2.org/a/b"
            }, task.TranslatedSourceRoots?.Select(r => r.GetMetadata("ScmRepositoryUrl")));

            Assert.True(result);
        }
    }
}
