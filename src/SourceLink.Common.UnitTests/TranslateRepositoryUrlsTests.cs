﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System.Linq;
using Microsoft.Build.Tasks.SourceControl;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.Common.UnitTests
{
    public class TranslateRepositoryUrlsTests
    {
        [Fact]
        public void Translate()
        {
            var engine = new MockEngine();

            var task = new TranslateRepositoryUrlsGitTask()
            {
                BuildEngine = engine,
                RepositoryUrl = "ssh://account@contoso.com/a/b",
                IsSingleProvider = true,
                SourceRoots = new[]
                {
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@contoso.com:123/a/b?x=y")),
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "http://user@contoso.com:123/a/b?x=y")),
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "https://user@contoso.com:123/a/b?x=y")),
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "git://contoso.com:123/a/b?x=y")),
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ftp://account@contoso.com:123/a/b?x=y")),    // unsupported protocol
                    new MockItem("/src/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://account@contoso.com:123/a/b?x=y")),   // different source control
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "ssh://account@contoso2.com:123/a/b?x=y")),   // unknown host
                },
                Hosts = new[]
                {
                    new MockItem("contoso.com")
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual("https://contoso.com/a/b", task.TranslatedRepositoryUrl);

            AssertEx.Equal(new[]
            {
                "https://contoso.com/a/b?x=y",
                "http://contoso.com:123/a/b?x=y",
                "https://contoso.com:123/a/b?x=y",
                "https://contoso.com/a/b?x=y",
                "ftp://account@contoso.com:123/a/b?x=y",
                "ssh://account@contoso.com:123/a/b?x=y",
                "ssh://account@contoso2.com:123/a/b?x=y"
            }, task.TranslatedSourceRoots.Select(r => r.GetMetadata("ScmRepositoryUrl")));

            Assert.True(result);
        }
    }
}
