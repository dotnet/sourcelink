# Source Link

Source Link is a language- and source-control agnostic system for providing first-class source debugging experiences for binaries. The goal of the project is to enable anyone building [NuGet libraries to provide source debugging](https://github.com/dotnet/designs/blob/main/accepted/2020/diagnostics/debugging-with-symbols-and-sources.md) for their users with almost no effort. Microsoft libraries, such as .NET Core and Roslyn have enabled Source Link. Source Link is supported by Microsoft.

Source Link [specification](https://github.com/dotnet/designs/blob/main/accepted/2020/diagnostics/source-link.md#source-link-file-specification) describes source control metadata that can be embedded in symbols, binaries and packages to link them to their original sources.

Visual Studio 15.3+ supports reading Source Link information from symbols while debugging. It downloads and displays the appropriate commit-specific source for users, such as from [raw.githubusercontent](https://raw.githubusercontent.com/dotnet/roslyn/681cbc414542ffb9fb13ded613d26a88ea73a44b/src/VisualStudio/Core/Def/Implementation/ProjectSystem/AbstractProject.cs), enabling breakpoints and all other sources debugging experience on arbitrary NuGet dependencies. Visual Studio 15.7+ supports downloading source files from private GitHub and Azure DevOps (former VSTS) repositories that require authentication.

The [original Source Link implementation](https://github.com/ctaggart/SourceLink) was provided by [@ctaggart](https://github.com/ctaggart). Thanks! The .NET Team and Cameron worked together to make this implementation available in the .NET Foundation.

> If you arrived here from the original Source Link documentation - you do not need to use `SourceLink.Create.CommandLine`.

## Using Source Link in .NET projects

Starting with .NET 8, Source Link for the following source control providers is included in the .NET SDK and enabled by default:
- [GitHub](http://github.com) or [GitHub Enterprise](https://enterprise.github.com/home) 
- [Azure Repos](https://azure.microsoft.com/en-us/services/devops/repos) git repositories (formerly known as Visual Studio Team Services)
- [GitLab](https://gitlab.com) 12.0+ (for older versions see [GitLab settings](#gitlab))
- [Bitbucket](https://bitbucket.org/) 4.7+ (for older versions see [Bitbucket settings](#bitbucket))

If your project uses .NET SDK 8+ and is hosted by the above providers it does not need to reference any Source Link packages or set any build properties.

**Otherwise**, you can enable Source Link experience in your project by setting a few properties and adding a PackageReference to a Source Link package specific to the provider:

```xml
<Project>
 <PropertyGroup>
    <!-- Optional: Publish the repository URL in the built .nupkg (in the NuSpec <Repository> element) -->
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
 
    <!-- Optional: Embed source files that are not tracked by the source control manager in the PDB -->
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  </PropertyGroup>
  <ItemGroup>
    <!-- Add PackageReference specific for your source control provider (see below) --> 
  </ItemGroup>
</Project>
```

Source Link packages are currently available for the source control providers listed below.

> Source Link package is a development dependency, which means it is only used during build. It is therefore recommended to set `PrivateAssets` to `all` on the package reference. This prevents consuming projects of your nuget package from attempting to install Source Link.

> Referencing any Source Link package in a .NET SDK 8+ project suppresses Source Link that is included in the SDK.

### github.com and GitHub Enterprise

For projects hosted by [GitHub](http://github.com) or [GitHub Enterprise](https://enterprise.github.com/home) reference 
[Microsoft.SourceLink.GitHub](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub) like so:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.GitHub" Version="8.0.0" PrivateAssets="All"/>
</ItemGroup>
```

### Azure Repos (former Visual Studio Team Services)

For projects hosted by [Azure Repos](https://azure.microsoft.com/en-us/services/devops/repos) in git repositories reference [Microsoft.SourceLink.AzureRepos.Git](https://www.nuget.org/packages/Microsoft.SourceLink.AzureRepos.Git): 

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.AzureRepos.Git" Version="8.0.0" PrivateAssets="All"/>
</ItemGroup>
```

### Azure DevOps Server (former Team Foundation Server)

For projects hosted by on-prem [Azure DevOps Server](https://azure.microsoft.com/en-us/services/devops/server/) in git repositories reference
[Microsoft.SourceLink.AzureDevOpsServer.Git](https://www.nuget.org/packages/Microsoft.SourceLink.AzureDevOpsServer.Git) and add host configuration like so:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.AzureDevOpsServer.Git" Version="8.0.0" PrivateAssets="All"/>
</ItemGroup>
```

You also need to provide the hostname of your DevOps server:
 
```xml
<ItemGroup>
  <SourceLinkAzureDevOpsServerGitHost Include="server-name"/>
</ItemGroup>
```
 
The `Include` attribute specifies the domain and optionally the port of the server (e.g. `server-name` or `server-name:8080`).

If your server is configured with a non-empty IIS [virtual directory](docs/TfsVirtualDirectory/README.md), specify this directory like so:

```xml
<ItemGroup>
  <SourceLinkAzureDevOpsServerGitHost Include="server-name" VirtualDirectory="tfs"/>
</ItemGroup>
```

### GitLab

For projects hosted by [GitLab](https://gitlab.com) reference [Microsoft.SourceLink.GitLab](https://www.nuget.org/packages/Microsoft.SourceLink.GitLab) package: 

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.GitLab" Version="8.0.0" PrivateAssets="All"/>
</ItemGroup>
```

Starting with version 8.0.0, Microsoft.SourceLink.GitLab assumes GitLab version 12.0+ by default.
If your project is hosted by GitLab older than version 12.0 you must specify `SourceLinkGitLabHost` item group in addition to the package reference:

```xml
<ItemGroup>
  <SourceLinkGitLabHost Include="gitlab.yourdomain.com" Version="11.0"/>
</ItemGroup>
```

The item group `SourceLinkGitLabHost` specifies the domain of the GitLab host and the version of GitLab.
The version is important since URL format for accessing files changes with version 12.0. By default Source Link assumes new format (version 12.0+).

You might also consider using environment variable [`CI_SERVER_VERSION`](https://docs.gitlab.com/ee/ci/variables/predefined_variables.html) (`Version="$(CI_SERVER_VERSION)"`) if available in your build environment.
 
### Bitbucket

For projects in git repositories hosted on [Bitbucket.org](https://bitbucket.org) or hosted on an on-prem Bitbucket server reference [Microsoft.SourceLink.Bitbucket.Git](https://www.nuget.org/packages/Microsoft.SourceLink.Bitbucket.Git) package:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.Bitbucket.Git" Version="8.0.0" PrivateAssets="All"/>
</ItemGroup>
```

If your project is hosted by Bitbucket Server or Bitbucket Data Center older than version 4.7 you must specify `SourceLinkBitbucketGitHost` item group in addition to the package reference:

```xml
<ItemGroup>
  <SourceLinkBitbucketGitHost Include="bitbucket.yourdomain.com" Version="4.5"/>
</ItemGroup>
```

The item group `SourceLinkBitbucketGitHost` specifies the domain of the Bitbucket host and the version of Bitbucket.
The version is important since URL format for accessing files changes with version 4.7. By default Source Link assumes new format (version 4.7+).

### gitweb

For projects hosted on-prem via [gitweb](https://git-scm.com/docs/gitweb) reference [Microsoft.SourceLink.GitWeb](https://www.nuget.org/packages/Microsoft.SourceLink.GitWeb) package: 

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.GitWeb" Version="8.0.0" PrivateAssets="All"/>
</ItemGroup>
```

### gitea

For projects hosted on-prem via [gitea](https://gitea.io) reference [Microsoft.SourceLink.Gitea](https://www.nuget.org/packages/Microsoft.SourceLink.Gitea) package: 

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.Gitea" Version="8.0.0" PrivateAssets="All"/>
</ItemGroup>
```

### Multiple providers, repositories with submodules

If your repository contains submodules hosted by multiple git providers reference packages of all these providers, unless the project uses .NET SDK 8+ and submodules only use providers for which Source Link support is included. For example, projects in a repository hosted by Azure Repos that links a GitHub repository via a submodule should reference both [Microsoft.SourceLink.AzureRepos.Git](https://www.nuget.org/packages/Microsoft.SourceLink.AzureRepos.Git) and [Microsoft.SourceLink.GitHub](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub) packages. [Additional configuration](https://github.com/dotnet/sourcelink/blob/main/docs/README.md#configuring-projects-with-multiple-sourcelink-providers) might be needed if multiple Source Link packages are used in the project.

## Using Source Link in C++ projects

Source Link package supports integration with VC++ projects (vcxproj) and VC++ linker.

To add Source Link support to your native project add package references corresponding to your source control provider to `packages.config` directly or using [NuGet Package Manager UI](https://docs.microsoft.com/en-us/nuget/tools/package-manager-ui) in Visual Studio. For example, the `packages.config` file for a project hosted on GitHub would include the following lines:  

```xml
<packages>
  <package id="Microsoft.Build.Tasks.Git" version="8.0.0" targetFramework="native" developmentDependency="true" />
  <package id="Microsoft.SourceLink.Common" version="8.0.0" targetFramework="native" developmentDependency="true" />
  <package id="Microsoft.SourceLink.GitHub" version="8.0.0" targetFramework="native" developmentDependency="true" />
</packages>
```

Once the packages are restored and the project built the Source Link information is [passed to the linker](https://docs.microsoft.com/en-us/cpp/build/reference/sourcelink) and embedded into the generated PDB.

The only feature currently supported is mapping of source files to the source repository that is used by the debugger to find source files when stepping into the code. Source embedding and embedding commit SHA and repository URL information in the native binary are not supported for native projects.

## Prerequisites for .NET projects

Source Link supports classic .NET Framework projects as well as .NET SDK projects, that is projects that import `Microsoft.NET.Sdk` (e.g. like so: `<Project Sdk="Microsoft.NET.Sdk">`). The project may target any .NET Framework or .NET Core App/Standard version. All PDB formats are supported: Portable, Embedded and Windows PDBs. 

[.NET Core SDK 2.1.300](https://www.microsoft.com/net/download/dotnet-core/sdk-2.1.300) or newer is required for .NET SDK projects. If building via desktop `msbuild` you'll need version 15.7 or higher.

The following features are not available in projects that do not import `Microsoft.NET.Sdk`:
- Automatic inclusion of commit SHA in `AssemblyInformationalVersionAttribute`.
- Automatic inclusion of commit SHA and repository URL in NuSpec.

These features can be added via custom msbuild targets.

## Prerequisites for C++ projects

Debugging native binary with Source Link information embedded in the PDB is supported since Visual Studio 2017 Update 9.

The VC++ linker supports `/SOURCELINK` [switch](https://docs.microsoft.com/en-us/cpp/build/reference/sourcelink) since Visual Studio 2017 Update 8, however the PDBs produced by this version are not compatible with case-sensitive source control systems such as git. This issue is fixed in [Visual Studio 2019](https://visualstudio.microsoft.com/vs/preview/).

## Known issues

- `EmbedUntrackedSources` does not work in Visual Basic projects that use .NET SDK: https://github.com/dotnet/sourcelink/issues/193 (fixed in Visual Studio 2019)
- Issue when building WPF projects with `/p:ContinuousIntegrationBuild=true`: https://github.com/dotnet/sourcelink/issues/91
- Issue when building WPF projects with embedding sources on and `BaseIntermediateOutputPath` not a subdirectory of the project directory: https://github.com/dotnet/sourcelink/issues/492

## PDB distributions

If you distribute the library via a package published to [NuGet.org](http://nuget.org), it is recommended to build a [symbol package](https://docs.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg) and publish it to [NuGet.org](http://nuget.org) as well. This will make the symbols available on [NuGet.org symbol server](https://docs.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg#nugetorg-symbol-server), where the debugger can download it from when needed.

Alternatively, Portable PDBs can be included in the main NuGet package by setting the following property in your project:

```xml
  <PropertyGroup>
    <AllowedOutputExtensionsInPackageBuildOutputFolder>$(AllowedOutputExtensionsInPackageBuildOutputFolder);.pdb</AllowedOutputExtensionsInPackageBuildOutputFolder>
  </PropertyGroup>  
```

Keep in mind that including PDBs in the .nupkg increases the size of the package and thus restore time for projects that consume your package, regardless of whether the user needs to debug through the source code of your library or not.

.snupkg symbol packages have following limitations:

- They do not support Windows PDBs (generated by VC++, or for managed projects that set build property `DebugType` to `full`)
- They require the library to be built by newer C#/VB compiler (Visual Studio 2017 Update 9).
- The consumer of the package also needs Visual Studio 2017 Update 9 debugger.
- Not supported by [Azure DevOps Artifacts](https://azure.microsoft.com/en-us/services/devops/artifacts) service.

Consider including PDBs in the main package if it is not possible to use .snupkg for the above reasons. 
For managed projects, consider switching to Portable PDBs by setting `DebugType` property to `portable`. This is the default for .NET SDK projects, but not classic .NET projects.

## Builds

Pre-release builds are available from Azure DevOps public feed: `https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet8/nuget/v3/index.json` ([browse](https://dev.azure.com/dnceng/public/_packaging?_a=feed&feed=dotnet8)).

[![Build Status](https://dnceng.visualstudio.com/public/_apis/build/status/SourceLink%20PR?branchName=main)](https://dnceng.visualstudio.com/public/_build/latest?definitionId=297?branchName=main)

## Experience in Visual Studio

The following screenshot demonstrates debugging a NuGet package referenced by an application, with source automatically downloaded from GitHub and used by Visual Studio 2017.

![sourcelink-example](https://user-images.githubusercontent.com/2608468/39667937-10d7dabe-5076-11e8-815e-935724b3a783.PNG)
