// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System.Linq;
using TestUtilities;
using Xunit;
using static TestUtilities.KeyValuePairUtils;

namespace Microsoft.SourceLink.GitWeb.UnitTests
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
                RepositoryUrl = "ssh://git@src.intranet.company.com/root_dir_name/sub_dirs/reponame.git",
                IsSingleProvider = true,
                SourceRoots = new[]
                {
                    new MockItem("/1/", KVP("SourceControl", "git"),  KVP("ScmRepositoryUrl", "ssh://git@src.intranet.company.com/root_dir_name/sub_dirs/reponame.git")),
                    new MockItem("/2/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://git@src.intranet.company1.com/root_dir_name/sub_dirs/reponame.git")),
                    new MockItem("/2/", KVP("SourceControl", "git"),  KVP("ScmRepositoryUrl", "ssh://git@src.intranet.company1.com/root_dir_name/sub_dirs/reponame.git")),
                    new MockItem("/2/", KVP("SourceControl", "tfvc"), KVP("ScmRepositoryUrl", "ssh://git@src.intranet.company2.com/root_dir_name/sub_dirs/reponame.git")),
                },
                Hosts = new[]
                {
                    new MockItem("src.intranet.company1.com")
                }
            };

            bool result = task.Execute();
            AssertEx.AssertEqualToleratingWhitespaceDifferences("", engine.Log);

            AssertEx.AreEqual("ssh://git@src.intranet.company.com/root_dir_name/sub_dirs/reponame.git", task.TranslatedRepositoryUrl);

            AssertEx.Equal(new[]
            {
                 "ssh://git@src.intranet.company.com/root_dir_name/sub_dirs/reponame.git",
                 "ssh://git@src.intranet.company1.com/root_dir_name/sub_dirs/reponame.git",
                 "ssh://git@src.intranet.company1.com/root_dir_name/sub_dirs/reponame.git",
                 "ssh://git@src.intranet.company2.com/root_dir_name/sub_dirs/reponame.git",
            }, task.TranslatedSourceRoots?.Select(r => r.GetMetadata("ScmRepositoryUrl")));

            Assert.True(result);
        }
    }
}
