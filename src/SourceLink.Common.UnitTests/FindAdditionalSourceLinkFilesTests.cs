// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.IO;
using TestUtilities;

using Xunit;

namespace Microsoft.SourceLink.Common.UnitTests
{
    public class FindAdditionalSourceLinkFilesTests
    {
        [Fact]
        public void NoSourceLinkFilesExpected()
        {
            var task = new FindAdditionalSourceLinkFiles()
            {
                SourceLinkFile = "merged.sourcelink.json",
                ImportLibraries = new string[] { },
                AdditionalDependencies = new string[] { }
                
            };

            bool result = task.Execute();

            Assert.NotNull(task.AllSourceLinkFiles);
            Assert.Single(task.AllSourceLinkFiles);
            Assert.True(result);
        }

        [Fact]
        public void FoundSourceLinkForImportLib()
        {
            string testLib = "test.lib";
            string testSourceLink = "test.sourcelink.json";

            using var temp = new TempRoot();
            var root = temp.CreateDirectory();
            var testDir = root.CreateDirectory("FoundSourceLinkForImportLib");
            var testLibFile = root.CreateFile(Path.Combine(testDir.Path, testLib));
            var testSourceLinkFile = root.CreateFile(Path.Combine(testDir.Path, testSourceLink));
            var task = new FindAdditionalSourceLinkFiles()
            {
                SourceLinkFile = "merged.sourcelink.json",
                ImportLibraries = new string[] { testLibFile.Path },
                AdditionalDependencies = new string[] { }

            };

            bool result = task.Execute();
            Assert.NotNull(task.AllSourceLinkFiles);
            Assert.NotEmpty(task.AllSourceLinkFiles);
#pragma warning disable CS8602 // Dereference of a possibly null reference - previously checked
            Assert.Equal(testSourceLinkFile.Path, task.AllSourceLinkFiles[1]);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            Assert.True(result);
        }

        [Fact]
        public void FoundSourceLinkForNonRootedAdditionalDependency()
        {
            string testLib = "test.lib";
            string testSourceLink = "test.sourcelink.json";

            using var temp = new TempRoot();
            var root = temp.CreateDirectory();
            var testDir = root.CreateDirectory("FoundSourceLinkForNonRootedAdditionalDependency");
            var testLibFile = root.CreateFile(Path.Combine(testDir.Path, testLib));
            var testSourceLinkFile = root.CreateFile(Path.Combine(testDir.Path, testSourceLink));
            var task = new FindAdditionalSourceLinkFiles()
            {
                SourceLinkFile = "merged.sourcelink.json",
                ImportLibraries = new string[] { },
                AdditionalDependencies = new string[] { testLib },
                AdditionalLibraryDirectories = new string[] { testDir.Path }
            };

            bool result = task.Execute();
            Assert.NotNull(task.AllSourceLinkFiles);
            Assert.NotEmpty(task.AllSourceLinkFiles);
#pragma warning disable CS8602 // Dereference of a possibly null reference - previously checked
            Assert.Equal(testSourceLinkFile.Path, task.AllSourceLinkFiles[1]);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            Assert.True(result);
        }

        [Fact]
        public void FoundSourceLinkForRootedAdditionalDependency()
        {
            string testLib = "test.lib";
            string testSourceLink = "test.sourcelink.json";

            using var temp = new TempRoot();
            var root = temp.CreateDirectory();
            var testDir = root.CreateDirectory("FoundSourceLinkForRootedAdditionalDependency");
            var testLibFile = root.CreateFile(Path.Combine(testDir.Path, testLib));
            var testSourceLinkFile = root.CreateFile(Path.Combine(testDir.Path, testSourceLink));
            var task = new FindAdditionalSourceLinkFiles()
            {
                SourceLinkFile = "merged.sourcelink.json",
                ImportLibraries = new string[] { },
                AdditionalDependencies = new string[] { testLibFile.Path },
                AdditionalLibraryDirectories = new string[] { }
            };

            bool result = task.Execute();

            Assert.NotNull(task.AllSourceLinkFiles);
            Assert.NotEmpty(task.AllSourceLinkFiles);
#pragma warning disable CS8602 // Dereference of a possibly null reference - previously checked
            Assert.Equal(testSourceLinkFile.Path, task.AllSourceLinkFiles[1]);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            Assert.True(result);
        }

        [Fact]
        public void SourceLinkError()
        {
            var task = new FindAdditionalSourceLinkFiles()
            {
                SourceLinkFile = "merged.sourcelink.json",
                ImportLibraries = new string[] { },
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - deliberate to cause error
                AdditionalDependencies = new string[] { null },
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
                AdditionalLibraryDirectories = new string[] { @"C:\Does\Not\Exist" }
            };

            bool result = task.Execute();
            Assert.NotNull(task.AllSourceLinkFiles);
            Assert.Empty(task.AllSourceLinkFiles);
            Assert.False(result);
        }
    }
}
