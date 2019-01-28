# Source Link (preview)

Source Link is a language- and source-control agnostic system for providing first-class source debugging experiences for binaries. The goal of the project is to enable anyone building [NuGet libraries to provide source debugging](https://github.com/dotnet/designs/blob/master/accepted/diagnostics/debugging-with-symbols-and-sources.md) for their users with almost no effort. Microsoft libraries, such as .NET Core and Roslyn have enabled Source Link. Source Link is supported by Microsoft.

Source Link is a [set of packages](https://www.nuget.org/packages?q=Microsoft.SourceLink) and a [specification](https://github.com/dotnet/designs/blob/master/accepted/diagnostics/source-link.md#source-link-file-specification) for describing source control metadata that can be embedded in symbols, binaries and packages.

Visual Studio 15.3+ supports reading Source Link information from symbols while debugging. It downloads and displays the appropriate commit-specific source for users, such as from [raw.githubusercontent](https://raw.githubusercontent.com/dotnet/roslyn/681cbc414542ffb9fb13ded613d26a88ea73a44b/src/VisualStudio/Core/Def/Implementation/ProjectSystem/AbstractProject.cs), enabling breakpoints and all other sources debugging experience on arbitrary NuGet dependencies. Visual Studio 15.7+ supports downloading source files from private GitHub and Azure DevOps (former VSTS) repositories that require authentication.

The [original Source Link implementation](https://github.com/ctaggart/SourceLink) was provided by [@ctaggart](https://github.com/ctaggart). Thanks! The .NET Team and Cameron worked together to make this implementation available in the .NET Foundation.

> If you arrived here from the original Source Link documentation - you do not need to use `SourceLink.Create.CommandLine`. You only need to install the packages listed below.

## <a name="using-sourcelink">Using Source Link in .NET projects

You can enable Source Link experience in your own .NET project by setting a few properties and adding a PackageReference to a Source Link package:

```xml
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
 
    <!-- Optional: Publish the repository URL in the built .nupkg (in the NuSpec <Repository> element) -->
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
 
    <!-- Optional: Embed source files that are not tracked by the source control manager in the PDB -->
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  
    <!-- Optional: Build symbol package (.snupkg) to distribute the PDB containing Source Link -->
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>
  <ItemGroup>
    <!-- Add PackageReference specific for your source control provider (see below) --> 
  </ItemGroup>
</Project>
```

If you distribute the library via a package published to [NuGet.org](http://nuget.org), it is recommended to build a [symbol package](https://docs.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg) and publish it to [NuGet.org](http://nuget.org) as well. This will make the symbols available on [NuGet.org symbol server](https://docs.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg#nugetorg-symbol-server), where the debugger can download it from when needed. Alternatively, you can [include the symbols in the main package](#alternative-pdb-distribution). However, doing so is not recommended as it increases the size of the package and thus restore time for projects that consume your package.

Source Link packages are currently available for the following source control providers.

> Source Link package is a development dependency, which means it is only used during build. It is therefore recommended to set `PrivateAssets` to `all` on the package reference. This prevents consuming projects of your nuget package from attempting to install Source Link.

### github.com and GitHub Enterprise

For projects hosted by [GitHub](http://github.com) or [GitHub Enterprise](https://enterprise.github.com/home) reference 
[Microsoft.SourceLink.GitHub](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub) like so:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.GitHub" Version="1.0.0-beta2-18618-05" PrivateAssets="All"/>
</ItemGroup>
```

### Azure DevOps (Visual Studio Team Services)

For projects hosted by [Azure DevOps](https://www.visualstudio.com/team-services) in git repositories reference [Microsoft.SourceLink.Vsts.Git](https://www.nuget.org/packages/Microsoft.SourceLink.Vsts.Git): 

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.Vsts.Git" Version="1.0.0-beta2-18618-05" PrivateAssets="All"/>
</ItemGroup>
```

### Team Foundation Server

For projects hosted by on-prem [Team Foundation Server](https://visualstudio.microsoft.com/tfs) in git repositories reference
[Microsoft.SourceLink.Tfs.Git](https://www.nuget.org/packages/Microsoft.SourceLink.Tfs.Git) and add TFS host configuration like so:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.Tfs.Git" Version="1.0.0-beta2-18618-05" PrivateAssets="All"/>
  <SourceLinkTfsGitHost Include="tfs-server-name" VirtualDirectory="tfs"/>
</ItemGroup>
```

`SourceLinkTfsGitHost` item specifies the domain and optionally the port of the TFS server (e.g. `myserver`, `myserver:8080`, etc.) and IIS virtual directory of the server (e.g. `tfs`).

### GitLab

For projects hosted by [GitLab](https://gitlab.com) reference [Microsoft.SourceLink.GitLab](https://www.nuget.org/packages/Microsoft.SourceLink.GitLab) package: 

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.GitLab" Version="1.0.0-beta2-18618-05" PrivateAssets="All"/>
</ItemGroup>
```

### Bitbucket.org

For projects hosted on [Bitbucket.org](https://bitbucket.org) in git repositories reference [Microsoft.SourceLink.Bitbucket.Git](https://www.nuget.org/packages/Microsoft.SourceLink.Bitbucket.Git) package: 

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SourceLink.Bitbucket.Git" Version="1.0.0-beta2-18618-05" PrivateAssets="All"/>
</ItemGroup>
```

### Multiple providers, repositories with submodules

If your repository contains submodules hosted by other git providers reference packages of all these providers. For example, projects in a repository hosted by Azure DevOps that links a GitHub repository via a submodule should reference both [Microsoft.SourceLink.Vsts.Git](https://www.nuget.org/packages/Microsoft.SourceLink.Vsts.Git) and [Microsoft.SourceLink.GitHub](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub) packages. [Additional configuration](https://github.com/dotnet/sourcelink/blob/master/docs/README.md#configuring-projects-with-multiple-sourcelink-providers) might be needed if multiple Source Link packages are used in the project.

## Using Source Link in C++ projects

Source Link package supports integration with VC++ projects (vcxproj) and VC++ linker.

To add Source Link support to your native project add package references corresponding to your source control provider to `packages.config` directly or using [NuGet Package Manager UI](https://docs.microsoft.com/en-us/nuget/tools/package-manager-ui) in Visual Studio. For example, the `packages.config` file for a project hosted on GitHub would include the following lines:  

```xml
<packages>
  <package id="Microsoft.Build.Tasks.Git" version="1.0.0-beta2-18618-05" targetFramework="native" developmentDependency="true" />
  <package id="Microsoft.SourceLink.Common" version="1.0.0-beta2-18618-05" targetFramework="native" developmentDependency="true" />
  <package id="Microsoft.SourceLink.GitHub" version="1.0.0-beta2-18618-05" targetFramework="native" developmentDependency="true" />
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

- `EmbedUntrackedSources` does not work in Visual Basic projects that use .NET SDK: https://github.com/dotnet/sourcelink/issues/193
- Various build issues on Mono: https://github.com/dotnet/sourcelink/issues/155
- Issues with WPF projects when building with `/p:ContinuousIntegrationBuild=true`: https://github.com/dotnet/sourcelink/issues/91

## Alternative PDB distribution

Prior availability of [NuGet.org symbol server](https://docs.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg#nugetorg-symbol-server) the recommendation used to be to include the PDB in the main NuGet package by setting the following property in your project:

```xml
  <PropertyGroup>
    <AllowedOutputExtensionsInPackageBuildOutputFolder>$(AllowedOutputExtensionsInPackageBuildOutputFolder);.pdb</AllowedOutputExtensionsInPackageBuildOutputFolder>`
  </PropertyGroup>  
```

Including PDBs in the .nupkg is generally no longer recommended as it increases the size of the package and thus restore time for projects that consume your package, regardless of whether the user needs to debug through the source code of your library or not. That said, .snupkg symbol packages have some limitations:

- They do not currently support Windows PDBs (generated by VC++, or for managed projects that set build property `DebugType` to `full`)
- They require the library to be built by newer C#/VB compiler (Visual Studio 2017 Update 9).
- The consumer of the package also needs Visual Studio 2017 Update 9 debugger.

Consider including PDBs in the main package only if it is not possible to use .snupkg for the above reasons. 
For managed projects, consider switching to Portable PDBs by setting `DebugType` property to `portable`. This is the default on .NET SDK projects, but not classic .NET projects.

## Builds

Pre-release builds are available on MyGet gallery: https://dotnet.myget.org/Gallery/sourcelink.

[![Build Status](https://dnceng.visualstudio.com/public/_apis/build/status/SourceLink%20PR?branchName=master)](https://dnceng.visualstudio.com/public/_build/latest?definitionId=297?branchName=master)

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Experience in Visual Studio

The following screenshot demonstrates debugging a NuGet package referenced by an application, with source automatically downloaded from GitHub and used by Visual Studio 2017.

![sourcelink-example](https://user-images.githubusercontent.com/2608468/39667937-10d7dabe-5076-11e8-815e-935724b3a783.PNG)
