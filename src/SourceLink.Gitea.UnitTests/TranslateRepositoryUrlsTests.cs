// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System.Linq;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Gitea.UnitTests
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
                RepositoryUrl = "ssh://gitea.com/a/b",
                IsSingleProvider = true,
                SourceRoots = new[]
                {
                    new MockItem("/1/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://gitea.com:22/a/b")),
                    new MockItem("/2/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://gitea1.com/a/b")),
                    new MockItem("/2/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://gitea1.com/a/b")),
                    new MockItem("/2/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://gitea2.com/a/b")),
                },
                Hosts = new[]
                {
                    new MockItem("gitea1.com")
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual("https://gitea.com/a/b", task.TranslatedRepositoryUrl);

            AssertEx.Equal(new[] 
            {
                "https://gitea.com/a/b",
                "ssh://gitea1.com/a/b",
                "https://gitea1.com/a/b",
                "ssh://gitea2.com/a/b"
            }, task.TranslatedSourceRoots?.Select(r => r.GetMetadata("ScmRepositoryUrl")));

            Assert.True(result);
        }
    }
}
