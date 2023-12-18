// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
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
        public void NoValidHosts()
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
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", "git://contoso.com:123/a/b?x=y"))
                },
                Hosts = new[]
                {
                    // host name must be domain:port 
                    new MockItem("http://contoso.com"),
                    new MockItem("contoso.com/abc"),
                    new MockItem("contoso.com:x")
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences(
               $"""
                WARNING : {string.Format(CommonResources.IgnoringInvalidHostName, "http://contoso.com")}
                WARNING : {string.Format(CommonResources.IgnoringInvalidHostName, "contoso.com/abc")}
                WARNING : {string.Format(CommonResources.IgnoringInvalidHostName, "contoso.com:x")}
                """, engine.Log);

            AssertEx.AreEqual("https://contoso.com/a/b", task.TranslatedRepositoryUrl);

            AssertEx.Equal(new[]
            {
                "https://contoso.com/a/b?x=y",
                "http://contoso.com:123/a/b?x=y",
                "https://contoso.com/a/b?x=y",
            }, task.TranslatedSourceRoots?.Select(r => r.GetMetadata("ScmRepositoryUrl")));

            Assert.True(result);
        }

        [Theory]
        [InlineData(TestStrings.GB18030)]
        [InlineData(TestStrings.GB18030Escaped)]
        [InlineData(TestStrings.GB18030FullyEscaped)]
        public void Translate(string name)
        {
            var engine = new MockEngine();

            // Make sure that we don't double-unescape ("%24%2572" -> "$%72" -> "$r").
            // The output of LocateRepository is an escaped ScmRepositoryUrl, which is then passed to the translation task.
            // However, we also test the case when the input of the translation task is unescaped.
            var inputRelativeUrl = "a/%24%2572/" + name;

            // The output of the translation task is escaped.
            var outputRelativeUrl = "a/%24%2572/" + TestStrings.GB18030Escaped;

            var task = new TranslateRepositoryUrlsGitTask()
            {
                BuildEngine = engine,
                RepositoryUrl = $"ssh://account@contoso.com/{inputRelativeUrl}",
                IsSingleProvider = true,
                SourceRoots = new[]
                {
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", $"ssh://account@contoso.com:123/{inputRelativeUrl}?x=y")),
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", $"http://user@contoso.com:123/{inputRelativeUrl}?x=y")),
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", $"https://user@contoso.com:123/{inputRelativeUrl}?x=y")),
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", $"git://contoso.com:123/{inputRelativeUrl}?x=y")),
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", $"ftp://account@contoso.com:123/{inputRelativeUrl}?x=y")),    // unsupported protocol
                    new MockItem("/src/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", $"ssh://account@contoso.com:123/{inputRelativeUrl}?x=y")),   // different source control
                    new MockItem("/src/", KVP("SourceControl", "git"), KVP("ScmRepositoryUrl", $"ssh://account@contoso2.com:123/{inputRelativeUrl}?x=y")),   // unknown host
                },
                Hosts = new[]
                {
                    new MockItem("contoso.com")
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual($"https://contoso.com/{outputRelativeUrl}", task.TranslatedRepositoryUrl);

            AssertEx.Equal(new[]
            {
                $"https://contoso.com/{outputRelativeUrl}?x=y",
                $"http://contoso.com:123/{outputRelativeUrl}?x=y",
                $"https://contoso.com:123/{outputRelativeUrl}?x=y",
                $"https://contoso.com/{outputRelativeUrl}?x=y",
                $"ftp://account@contoso.com:123/{inputRelativeUrl}?x=y",  // unchanged (translation not applied)
                $"ssh://account@contoso.com:123/{inputRelativeUrl}?x=y",  // unchanged (translation not applied)
                $"ssh://account@contoso2.com:123/{inputRelativeUrl}?x=y", // unchanged (translation not applied)
            }, task.TranslatedSourceRoots?.Select(r => r.GetMetadata("ScmRepositoryUrl")));

            Assert.True(result);
        }
    }
}
