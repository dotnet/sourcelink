## Source Control and Source Link Packages

MSBuild has no built-in knowledge of any source control system or provider. In order to retrieve information from the source control the project needs to include 
a package reference to the appropriate source control package. Microsoft provides source control packages for Git and TFVC managers:

 - Microsoft.Build.Tasks.Git (release)
 - Microsoft.Build.Tasks.Tfvc (experimental)
  
These packages implement a protocol defined by MSBuild that allows extraction of necessary information from the source control system during build.
This protocol can be implemented by third party packages in order to support other source control systems.

Adding one of these packages to the project enables MSBuild to automatically detect the current source revision id (Git commit hash, TFS shelveset number),
the repository URL, source files that are not tracked by the source control and other useful information. 

Having this information available enables the following features:

1) Including source revision id in `AssemblyInformationalVersionAttribute` and in NuSpec of the package produced by the project.
2) Automatic detection and publishing of the repository URL.
3) Embedding sources to the PDB that are not tracked by source control.
4) Generating [Source Link](https://github.com/dotnet/designs/blob/master/accepted/diagnostics/source-link.md) that 
   enables debuggers to find sources when stepping through the DLL/EXE produced by the project.

To generate Source Link having just the source control package is not sufficient, since various source control providers (hosts) 
differ in the way how they expose the content of the hosted repositories. A package specific to the provider is needed. 
The following Source Link packages have been released by Microsoft:

- Microsoft.SourceLink.GitHub (depends on Microsoft.Build.Tasks.Git package)
- Microsoft.SourceLink.AzureRepos.Git (depends on Microsoft.Build.Tasks.Git package)
- Microsoft.SourceLink.AzureDevOpsServer.Git (depends on Microsoft.Build.Tasks.Git package)
- Microsoft.SourceLink.GitLab (depends on Microsoft.Build.Tasks.Git package)
- Microsoft.SourceLink.Bitbucket.Git (depends on Microsoft.Build.Tasks.Git package)

In addition an experimental package is available for TFVC server:
- Microsoft.SourceLink.AzureRepos.Tfvc (depends on experimental Microsoft.Build.Tasks.Tfvc package)

The system is extensible and custom packages that handle other source control providers can be developed and used. See [Custom Source Link packages](#creating-custom-source-link-packages) for details.

Each Source Link package depends on the corresponding source control package. Referencing a Source Link package makes the dependent source control package also referenced, thus providing the other source control features to the project.

Note that it is possible and supported to reference multiple Source Link packages in a single project provided they depend on the same source control package. This is necessary when the project sources are stored in mutliple submodules hosted by different providers (e.g. Azure Repos repository containing a GitHub submodule). See [Configuring Projects with Multiple Source Link Providers](#configuring-projects-with-multiple-source-link-providers) for details.

## Basic Settings

### PublishRepositoryUrl

The URL of the repository supplied by the CI server or retrieved from source control manager is stored in `PrivateRepositoryUrl` variable.

This value is not directly embedded in build outputs to avoid inadvertently publishing links to private repositories.
Instead, `PublishRepositoryUrl` needs to be set by the project in order to publish the URL into `RepositoryUrl` property,
which is used e.g. in the nuspec file generated for NuGet package produced by the project.

### EmbedAllSources

Set `EmbedAllSources` to `true` to instruct the build system to embed all project source files into the generated PDB.

### EmbedUntrackedSources

Set `EmbedUntrackedSources` to `true` to instruct the build system to embed project source files that are not tracked by the source control or imported from a source package to the generated PDB.

Has no effect if `EmbedAllSources` is true.

If the project generates additional source files that are added to `Compile` item group in a custom target, this target must run before `BeforeCompile` target (specify `BeforeTargets="BeforeCompile"`). 
Otherwise, these additional source files will not be automatically embedded into the PDB. 

### ContinuousIntegrationBuild

Set `ContinuousIntegrationBuild` to `true` to indicate that the build executes on a build/CI server. 

`ContinuousIntegrationBuild` variable is used within the build system to enable settings that only apply to official builds, as opposed to local builds on developer machine. An example of such setting is [DeterministicSourcePaths](#deterministicsourcepaths).

### DeterministicSourcePaths

By setting `DeterministicSourcePaths` to true the project opts into mapping all source paths included in the project outputs to deterministic values, i.e. values that do not depend on the exact location of the project sources on disk, or the operating system on which the project is being built. 

Only set `DeterministicSourcePaths` to true on a build/CI server, never for local builds.
In order for the debugger to find source files when debugging a locally built binary, the PDB must contain original, unmapped local paths.

Starting with .NET Core SDK 2.1.300, a fully deterministic build is [turned on](https://github.com/dotnet/roslyn/blob/Visual-Studio-2019-Version-16.7.3/src/Compilers/Core/MSBuildTask/Microsoft.Managed.Core.targets#L131-L141) when both `Deterministic` and `ContinuousIntegrationBuild` properties are set to `true`. 

## Example

The following project settings result in repository URL and commit hash automatically detected and included in NuSpec, commit hash included in `AssemblyInformationalVersionAttribute`, all source files available on GitHub linked via [Source Link](https://github.com/dotnet/designs/blob/master/accepted/diagnostics/source-link.md) (including those in submodules) and source files not available on GitHub embedded in the PDB.

Note that .NET Core SDK 2.1.300 is required.

```xml
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup>
    <TargetFramework>netcoreapp2.1</TargetFramework>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="1.0.0" PrivateAssets="All"/>
  </ItemGroup>
</Project>
```

## Configuring Projects with Multiple Source Link Providers

An additional configuration might be required when a project references multiple Source Link packages and the source control is hosted on a custom domain. By default a Source Link package infers the domain of the source control host from the `origin` remote URL. For example, when a GitLab repository is cloned from `http://git.contoso.com` and a project only references Microsoft.SourceLink.GitLab package the package infers that `git.contoso.com` must be a hosting GitLab service. However, when multiple packages are used in the project it's not clear which service is `git.contoso.com` domain hosting.

### Custom Host Domains

Each package defines an msbuild item group named `SourceLink{provider}Host`, where `{provider}` is the source control provider the package is built for (e.g. `SourceLinkGitHubHost`, `SourceLinkGitLabHost`, `SourceLinkVstsGitHost` etc.). The item group allows the project to specify domain(s) that correspond to the source control provider of the package. For example, the following setting assigns `git.contoso.com` domain to GitHub package:

```xml
<ItemGroup>
  <SourceLinkGitHubHost Include="git.contoso.com"/>
</ItemGroup>
```

### Custom Content URLs

*Content URL* is the URL where the raw source files can be downloaded from. Items of `SourceLink*Host` item group allow to specify the content URL if necessary. The content URL doesn't need to be specified in most cases as it is inferred from the domain.

The default content URLs for each package is listed below ([GetSourceLinkUrlGitTask.GetDefaultContentUriFromHostUri](https://github.com/dotnet/sourcelink/blob/master/src/Common/GetSourceLinkUrlGitTask.cs#L50) API):

|                  | content URL        |
|:----------------:|:------------------:|
|**GitLab**        |https://{domain}    |
|**GitHub**        |https://{domain}/raw|
|**AzureRepos.Git**|https://{domain}    |
|**BitBucket**     |https://{domain}    |

To override the above defaults specify `ContentUrl` metadata on the item in [`build/{PackageName}.props`](https://github.com/dotnet/sourcelink/blob/master/src/SourceLink.GitHub/build/Microsoft.SourceLink.GitHub.props) in the Source Link package. For example, GitHub.com server provides content on a CDN domain `raw.githubusercontent.com`:
 
```xml
<ItemGroup>
  <SourceLinkGitHubHost Include="github.com" ContentUrl="https://raw.githubusercontent.com"/>
</ItemGroup>
```

## Advanced Settings and Concepts

### IncludeSourceRevisionInInformationalVersion

When `true` and a source control package is present the `SourceRevisionId` is included in the `AssemblyInformationalVersionAttribute`. 
The default value is `true`. Set to `false` to suppress publishing `SourceRevisionId` to the attribute.

### SourceRevisionId

Set by target `SetSourceRevisionId` and consumed by NuGet `Pack` target and `GenerateAssemblyInfo` target. 
May be used by custom targets that need this information.

### EnableSourceLink

This property is implicitly set to `true` by a Source Link package. Including a Source Link package thus enables Source Link generation unless explicitly disabled by the project by setting this property to `false`.

### SourceRoot

Item group that lists all source roots that the project source files reside under and their mapping to source control server URLs. This includes both source files under source control as well as source files in source packages.

Source root metadata:

- _Identity_: full path to the source root directory ending with a directory separator

All source control roots have the following metadata:

- _SourceControl_: the name of source control system, if the directory is a source source control root (e.g. `git`, `tfvc`, etc.)
- _RevisionId_: revision id (e.g. git commit hash)

Additional source-control specific metadata may be defined (depends on the source control system). 

For example, for Git:

- _RepositoryUrl_: e.g. `http://github.com/dotnet/corefx`

For TFVC:

- _CollectionUrl_
- _ProjectId_
- _ServerPath_

Nested source control roots have the following metadata (e.g. submodules):

- _NestedRoot_: URL to the source root relative to the containing source root (e.g. `src/submodules/mysubmodule`)
- _ContainingRoot_: the identity if the containing source root

Source roots not under source control:
- _SourceLinkUrl_: URL to use in source link mapping, including `*` wildcard (e.g. `https://raw.githubusercontent.com/dotnet/roslyn/42abf2e6642db97d2314c017eb179075d5042028/src/Dependencies/CodeAnalysis.Debugging/*`)

## Creating Custom Source Link Packages

Each Source Link package is expected to provide mapping of _repository URLs_ to corresponding _content URLs_ that provide source file content.

The content URL shall identify an end-point that responds to HTTP GET request with content of the source file identified in the query. The end-point may require authentication. 

The package shall depend on an appropriate source-control package (Microsoft.Build.Tasks.Git, Microsoft.Build.Tasks.Tfvc, etc.).

The package shall include `build/{PackageName}.props` and `build/{PackageName}.targets` files that get automatically included by NuGet into the project that references the package. 

`build/{PackageName}.props` file shall set `EnableSourceLink` property to `true` if it hasn't been set already, like so:

```xml
<PropertyGroup>
  <EnableSourceLink Condition="'$(EnableSourceLink)' == ''">true</EnableSourceLink>
</PropertyGroup> 
```

`build/{PackageName}.targets` file shall add a uniquely named Source Link initialization target to `SourceLinkUrlInitializerTargets` property, e.g. `_InitializeXyzSourceLinkUrl` for source control provider called `Xyz`.

```xml
<PropertyGroup>
  <SourceLinkUrlInitializerTargets>$(SourceLinkUrlInitializerTargets);_InitializeXyzSourceLinkUrl</SourceLinkUrlInitializerTargets>
</PropertyGroup>
```

The initialization target shall update each item of the `SourceRoot` item group that belongs to the Xyz provider with `SourceLinkUrl` metadata that contains the final URL for this source root that will be stored in the Source Link file. It shall ignore any `SourceRoot` items whose `SourceControl` and `RepositoryUrl` metadata it does not recognize.

See [the implementation of GitHub Source Link package](https://github.com/dotnet/sourcelink/blob/master/src/SourceLink.GitHub/build/Microsoft.SourceLink.GitHub.targets) for an example.

## Minimal git repository metadata

In some scenarios it is desirable to build a repository from a directory that contains its sources but does not have `.git` 
directory containing git metadata. Source Link does not require all git metadata to be present in the `.git` directory, but 
some metadata are needed. The following list describes the minimal set of directories and files that must be present in order 
for Source Link to operate properly.

- `.git/HEAD`

  text file containing a commit SHA (e.g. `935f4b5c55167d9e4ed99b753f7340999d66d5de`)

- `.git/config`

  configuration file that specifies `origin` remote URL (e.g. `[remote "origin"] url="http://server.com/repo"`)

If the repository has submodules the file `.gitmodules` must be present in the repository root and must list the 
relative paths of all submodules:

```
[submodule "submodule-name"]
  path = submodule-path
```

The `.git/config` file must contain URLs of all initialized submodules:

```
[submodule "submodule-name"]
  url = https://server.com/subrepo
```

The following additional files and directories must be present for each submodule:

- `.git/modules/submodule-name/HEAD`

  text file containing a commit SHA of the submodule

- `submodule-path/.git`

  text file pointing to the submodule metadata, with the following contents: `gitdir: ../.git/modules/submodule-name`
