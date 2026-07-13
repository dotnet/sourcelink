// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Linq;
using Microsoft.Build.Framework;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GetUntrackedFilesTests
    {
        /// <summary>
        /// A fully-qualified <see cref="GetUntrackedFiles.ProjectDirectory"/> resolves the relative file item
        /// specs, only the repository-ignored file is reported, and the original ItemSpec is preserved.
        /// </summary>
        [Fact]
        public void AbsoluteProjectDirectory_ReportsIgnoredFileAsUntracked()
        {
            using var temp = new TempRoot();
            var repoDir = CreateRepository(temp);

            var engine = new MockEngine();
            var task = new GetUntrackedFiles
            {
                BuildEngine = engine,
                ConfigurationScope = "local",
                ProjectDirectory = repoDir.Path,
                Files = new ITaskItem[]
                {
                    new MockItem("ignored_file.cs"),
                    new MockItem("included_file.cs"),
                },
            };

            Assert.True(task.Execute());
            Assert.DoesNotContain("ERROR", engine.Log);
            Assert.NotNull(task.UntrackedFiles);
            AssertEx.Equal(
                new[] { MockItem.AdjustSeparators("ignored_file.cs") },
                task.UntrackedFiles.Select(item => item.ItemSpec));
        }

        /// <summary>
        /// On the cached-repository path (RepositoryId set) the base <see cref="RepositoryTask"/> skips path
        /// validation, so <see cref="GetUntrackedFiles"/> must reject a relative
        /// <see cref="GetUntrackedFiles.ProjectDirectory"/> itself: a LocateRepository run primes the cache, then
        /// a GetUntrackedFiles run reusing it with a relative directory fails with an error.
        /// </summary>
        [Fact]
        public void RelativeProjectDirectory_OnCachedRepository_IsRejected()
        {
            using var temp = new TempRoot();
            var repoDir = CreateRepository(temp);

            var engine = new MockEngine();

            // Prime the repository cache and obtain the RepositoryId used as the cache key.
            var locate = new LocateRepository
            {
                BuildEngine = engine,
                ConfigurationScope = "local",
                NoWarnOnMissingInfo = true,
                Path = repoDir.Path,
            };
            Assert.True(locate.Execute());
            Assert.NotNull(locate.RepositoryId);

            var task = new GetUntrackedFiles
            {
                BuildEngine = engine,
                ConfigurationScope = "local",
                RepositoryId = locate.RepositoryId,
                ProjectDirectory = ".",
                Files = new ITaskItem[] { new MockItem("ignored_file.cs") },
            };

            Assert.False(task.Execute());
            Assert.Contains(Resources.PathMustBeAbsolute, engine.Log);
            Assert.Null(task.UntrackedFiles);
        }

        private static TempDirectory CreateRepository(TempRoot temp)
        {
            // A real on-disk git repository that ignores 'ignored_file.cs' but not 'included_file.cs'.
            var repoDir = temp.CreateDirectory();
            var gitDir = repoDir.CreateDirectory(".git");
            gitDir.CreateFile("HEAD").WriteAllText("ref: refs/heads/master");
            gitDir.CreateFile("config").WriteAllText("");
            gitDir.CreateDirectory("objects");
            gitDir.CreateDirectory("refs").CreateDirectory("heads").CreateFile("master").WriteAllText("0000000000000000000000000000000000000000");

            repoDir.CreateFile(".gitignore").WriteAllText("ignored_file.cs\n");
            repoDir.CreateFile("ignored_file.cs").WriteAllText("");
            repoDir.CreateFile("included_file.cs").WriteAllText("");
            return repoDir;
        }
    }
}
