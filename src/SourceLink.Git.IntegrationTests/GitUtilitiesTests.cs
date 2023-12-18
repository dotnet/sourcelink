// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.IO;
using System.Linq;
using TestUtilities;
using Xunit;

namespace Microsoft.SourceLink.IntegrationTests;

/// <summary>
/// Validata that LibGitSharp handles Unicode correctly.
/// </summary>
public sealed class GitUtilitiesTests
{
    public readonly TempRoot Temp = new TempRoot();

    [Fact]
    public void TestCreateGitRepository()
    {
        var dirName = $"d_{TestStrings.GB18030}";
        var fileName = $"f_{TestStrings.GB18030}";
        var url = $"https://github.com/%24%2572%2F{TestStrings.GB18030}";

        var repoDir = Temp.CreateDirectory().CreateDirectory(dirName);
        repoDir.CreateFile(fileName).WriteAllText("Hello");
        var repository = GitUtilities.CreateGitRepository(repoDir.Path, [fileName], url);

        var configLines = File.ReadAllLines(Path.Combine(repoDir.Path, ".git", "config"));
        var actualUrl = configLines.Single(l => l.Contains("url = ")).TrimStart().Substring("url = ".Length);
        AssertEx.AreEqual(url, actualUrl);
    }
}
