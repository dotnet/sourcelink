// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using LibGit2Sharp;
using TestUtilities;
using Xunit;

namespace Microsoft.SourceLink.IntegrationTests
{
    public class TargetTests : DotNetSdkTestBase
    {
        public TargetTests()
            : base("Microsoft.SourceLink.GitHub")
        {
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void GenerateSourceLinkFileTarget_EnableSourceLinkCondition()
        {
            GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, "http://github.com/test-org/test-repo");

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <Test_DefaultEnableSourceControlManagerQueries>$(EnableSourceControlManagerQueries)</Test_DefaultEnableSourceControlManagerQueries>
  <Test_DefaultEnableSourceLink>$(EnableSourceLink)</Test_DefaultEnableSourceLink>
</PropertyGroup>

",
                customTargets: @"
<Target Name=""Test_SetEnableSourceLink"" BeforeTargets=""InitializeSourceControlInformation"">
  <PropertyGroup>
    <EnableSourceLink>false</EnableSourceLink>
  </PropertyGroup>
</Target>
",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "$(Test_DefaultEnableSourceControlManagerQueries)",
                    "$(Test_DefaultEnableSourceLink)",
                    "$(SourceLink)"
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders,
                    ProjectSourceRoot,
                    "true",
                    "true",
                    ""
                });
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void DefaultValuesForEnableProperties_DesignTimeBuild()
        {
            GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, "http://github.com/test-org/test-repo");

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <Test_DefaultEnableSourceControlManagerQueries>$(EnableSourceControlManagerQueries)</Test_DefaultEnableSourceControlManagerQueries>
  <Test_DefaultEnableSourceLink>$(EnableSourceLink)</Test_DefaultEnableSourceLink>
</PropertyGroup>

",
                customTargets: "",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "$(Test_DefaultEnableSourceControlManagerQueries)",
                    "$(Test_DefaultEnableSourceLink)",
                    "$(SourceLink)"
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders,
                    "",
                    "",
                    ""
                },
                additionalCommandLineArgs: "/p:DesignTimeBuild=true");
        }

        [ConditionalFact(typeof(DotNetSdkAvailable))]
        public void DefaultValuesForEnableProperties_BuildingForLiveUnitTesting()
        {
            GitUtilities.CreateGitRepositoryWithSingleCommit(ProjectDir.Path, new[] { ProjectFileName }, "http://github.com/test-org/test-repo");

            VerifyValues(
                customProps: @"
<PropertyGroup>
  <Test_DefaultEnableSourceControlManagerQueries>$(EnableSourceControlManagerQueries)</Test_DefaultEnableSourceControlManagerQueries>
  <Test_DefaultEnableSourceLink>$(EnableSourceLink)</Test_DefaultEnableSourceLink>
</PropertyGroup>

",
                customTargets: "",
                targets: new[]
                {
                    "Build"
                },
                expressions: new[]
                {
                    "@(SourceRoot)",
                    "$(Test_DefaultEnableSourceControlManagerQueries)",
                    "$(Test_DefaultEnableSourceLink)",
                    "$(SourceLink)"
                },
                expectedResults: new[]
                {
                    NuGetPackageFolders,
                    "",
                    "",
                    ""
                },
                additionalCommandLineArgs: "/p:BuildingForLiveUnitTesting=true");
        }
    }
}
