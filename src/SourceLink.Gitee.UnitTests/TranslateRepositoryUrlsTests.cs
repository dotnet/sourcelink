// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System.Linq;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Gitee.UnitTests
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
                RepositoryUrl = "ssh://gitee.com/a/b",
                IsSingleProvider = true,
                SourceRoots = new[]
                {
                    new MockItem("/1/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://gitee.com:22/a/b")),
                    new MockItem("/2/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://gitee1.com/a/b")),
                    new MockItem("/2/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://gitee1.com/a/b")),
                    new MockItem("/2/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://gitee2.com/a/b")),
                },
                Hosts = new[]
                {
                    new MockItem("gitee1.com")
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual("https://gitee.com/a/b", task.TranslatedRepositoryUrl);

            AssertEx.Equal(new[]
            {
                "https://gitee.com/a/b",
                "ssh://gitee1.com/a/b",
                "https://gitee1.com/a/b",
                "ssh://gitee2.com/a/b"
            }, task.TranslatedSourceRoots?.Select(r => r.GetMetadata("ScmRepositoryUrl")));

            Assert.True(result);
        }
    }
}
