// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Gitea.UnitTests
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
                "ERROR : " + string.Format(CommonResources.AtLeastOneRepositoryHostIsRequired, "SourceLinkGiteaHost", "Gitea"), engine.Log);

            Assert.False(result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("", "/")]
        [InlineData("/", "")]
        [InlineData("/", "/")]
        public void BuildSourceLinkUrl(string s1, string s2)
        {
            var engine = new MockEngine();

            var task = new GetSourceLinkUrl()
            {
                BuildEngine = engine,
                SourceRoot = new MockItem("/src/", KVP("RepositoryUrl", "http://subdomain.mygitea.com:100/a/b" + s1), KVP("SourceControl", "git"), KVP("RevisionId", "0123456789abcdefABCDEF000000000000000000")),
                Hosts = new[]
                {
                    new MockItem("mygitea.com", KVP("ContentUrl", "https://domain.com/x/y" + s2)),
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);
            AssertEx.AreEqual("https://domain.com/x/y/a/b/raw/commit/0123456789abcdefABCDEF000000000000000000/*", task.SourceLinkUrl);
            Assert.True(result);
        }
    }
}
