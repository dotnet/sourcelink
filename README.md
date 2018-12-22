# SourceLink (preview)

SourceLink is a language- and source-control agnostic system for providing first-class source debugging experiences for binaries. The goal of the project is to enable anyone building [NuGet libraries to provide source debugging](https://github.com/dotnet/designs/blob/master/accepted/diagnostics/debugging-with-symbols-and-sources.md) for their users with almost no effort. Microsoft libraries, such as .NET Core and Roslyn have enabled SourceLink. SourceLink is supported by Microsoft.

SourceLink is a [set of packages](https://www.nuget.org/packages?q=Microsoft.SourceLink) and a [specification](https://github.com/dotnet/designs/blob/master/accepted/diagnostics/source-link.md#source-link-file-specification) for describing source control metadata that can be embedded in symbols, binaries and packages.

Visual Studio 15.3+ supports reading SourceLink information from symbols while debugging. It downloads and displays the appropriate commit-specific source for users, such as from [raw.githubusercontent](https://raw.githubusercontent.com/dotnet/roslyn/681cbc414542ffb9fb13ded613d26a88ea73a44b/src/VisualStudio/Core/Def/Implementation/ProjectSystem/AbstractProject.cs), enabling breakpoints and all other sources debugging experience on arbitrary NuGet dependencies. Visual Studio 15.7+ supports downloading source files from private GitHub and Azure DevOps (former VSTS) repositories that require authentication.

The [original SourceLink implementation](https://github.com/ctaggart/SourceLink) was provided by [@ctaggart](https://github.com/ctaggart). Thanks! The .NET Team and Cameron worked together to make this implementation available in the .NET Foundation.

> If you arrived here from the original sourcelink documentation - you do not need to use `SourceLink.Create.CommandLine`. You only need to install the packages listed below.

## Using SourceLink

You can enable SourceLink experience in your own project by setting a few properties and adding a PackageReference to a SourceLink package:

```xml
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
 
    <!-- Optional: Publish the repository URL in the built .nupkg (in the NuSpec <Repository> element) -->
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
 
    <!-- Optional: Embed source files that are not tracked by the source control manager in the PDB -->
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  
    <!-- Optional: Build symbol package (.snupkg) to distribute PDBs with SourceLink -->
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>
  <ItemGroup>
    <!-- Add PackageReference specific for your source control provider (see below) --> 
  </ItemGroup>
</Project>
```

If you distribute the library via a package published to [NuGet.org](nuget.org), it is recommended to build a [symbol package](https://docs.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg) and publish it to [NuGet.org](nuget.org) as well.

SourceLink packages are currently available for the following source control providers.

> SourceLink package is a development dependency, which means it is only used during build. It is therefore recommended to set `PrivateAssets` to `all` on the package reference. This prevents consuming projects of your nuget package from attempting to install SourceLink.

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

If your repository contains submodules hosted by other git providers reference packages of all these providers. For example, projects in a repository hosted by Azure DevOps that links a GitHub repository via a submodule should reference both [Microsoft.SourceLink.Vsts.Git](https://www.nuget.org/packages/Microsoft.SourceLink.Vsts.Git) and [Microsoft.SourceLink.GitHub](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub) packages. [Additional configuration](https://github.com/dotnet/sourcelink/blob/master/docs/README.md#configuring-projects-with-multiple-sourcelink-providers) might be needed if multiple SourceLink packages are used in the project.

## Prerequisites

Note that [.NET Core SDK 2.1.300](https://www.microsoft.com/net/download/dotnet-core/sdk-2.1.300) or newer is required for SourceLink to work. If building via desktop msbuild (as opposed to `dotnet build`) you'll need version 15.7.

## Known Issues

- `EmbedUntrackedSources` does not work in Visual Basic projects that use .NET SDK: https://github.com/dotnet/sourcelink/issues/193
- Various build issues on Mono : https://github.com/dotnet/sourcelink/issues/155
- Issues with OpenSSL dependency on some Linux distros: https://github.com/dotnet/sourcelink/issues/192
- Issues with WPF projects when building with `/p:ContinuousIntegrationBuild=true`: https://github.com/dotnet/sourcelink/issues/91

## Builds

Pre-release builds are available on MyGet gallery: https://dotnet.myget.org/Gallery/sourcelink.

[![Build Status](https://dnceng.visualstudio.com/public/_apis/build/status/SourceLink%20PR?branchName=master)](https://dnceng.visualstudio.com/public/_build/latest?definitionId=297?branchName=master)

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Experience in Visual Studio

The following screenshot demonstrates debugging a NuGet package referenced by an application, with source automatically downloaded from GitHub and used by Visual Studio 2017.

![sourcelink-example](https://user-images.githubusercontent.com/2608468/39667937-10d7dabe-5076-11e8-815e-935724b3a783.PNG)
