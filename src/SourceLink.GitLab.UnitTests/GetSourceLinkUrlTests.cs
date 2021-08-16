// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.GitLab.UnitTests
{
    public class GetSourceLinkUrlTests
    {
        [Fact]
        public void EmptyHosts()
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("x", KVP("RepositoryUrl", "http://abc.com"), KVP("SourceControl", "git")),
            };

            bool result = task.Execute();

            AssertEx.AssertEqualToleratingWhitespaceDifferences(
                "ERROR : " + string.Format(CommonResources.AtLeastOneRepositoryHostIsRequired, "SourceLinkGitLabHost", "GitLab"), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("", "", null)]
        [InlineData("", "/", null)]
        [InlineData("/", "", null)]
        [InlineData("/", "/", null)]
        [InlineData("", "", "12.0")]
        [InlineData("", "/", "12.0")]
        [InlineData("/", "", "12.0")]
        [InlineData("/", "/", "12.0")]
        public void BuildSourceLinkUrl(string s1, string s2, string version)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com:100/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    version == null
                        ? new MockItem("mygitlab.com", KVP("ContentUrl", "https://domain.com/x/y" + s2))
                        : new MockItem("mygitlab.com", KVP("ContentUrl", "https://domain.com/x/y" + s2), KVP("Version", version)),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com/x/y/a/b/-/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }

        [Theory]
        [InlineData("", "/", "11.0")]
        [InlineData("/", "", "11.0")]
        [InlineData("/", "/", "11.0")]
        public void BuildSourceLinkUrl_DeprecatedVersion(string s1, string s2, string version)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitlab.com:100/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygitlab.com", KVP("ContentUrl", "https://domain.com/x/y" + s2), KVP("Version", version)),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com/x/y/a/b/raw/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }
    }
}
