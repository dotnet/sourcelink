// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System.IO;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitEnvironmentTests
    {
        [ConditionalTheory(typeof(UnixOnly))]
        [InlineData(null, "/etc")]
        [InlineData("", "/etc")]
        [InlineData("/xyz", "/xyz")]
        public void FindSystemDirectory_Unix(string etc, string expected)
        {
            Assert.Equal(expected, GitEnvironment.FindSystemDirectory(null, etc));
        }

        [ConditionalFact(typeof(WindowsOnly))]
        public void FindSystemDirectory_Windows()
        {
            using var temp = new TempRoot();

            var base1 = temp.CreateDirectory();
            var base2 = temp.CreateDirectory();
            var d3 = temp.CreateDirectory();
            var d1 = base1.CreateDirectory("1");
            d1.CreateFile("git.cmd");
            var d2 = base2.CreateDirectory("2");
            d2.CreateFile("git.exe");

            Assert.Null(GitEnvironment.FindSystemDirectory(null, null));
            Assert.Null(GitEnvironment.FindSystemDirectory("", null));
            Assert.Null(GitEnvironment.FindSystemDirectory(";", null));
            Assert.Equal(GitEnvironment.FindSystemDirectory($"*;<>;  ;;{d3};{d1}", ""), Path.Combine(base1.Path, "etc"));
            Assert.Equal(GitEnvironment.FindSystemDirectory($"*;<>;\t;;{d3};{d2}", ""), Path.Combine(base2.Path, "etc"));
        }
    }
}
