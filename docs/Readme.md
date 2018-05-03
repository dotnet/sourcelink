## Source Control and Source Link Packages

MSBuild has no built-in knowledge of any source control system or provider. In order to retrieve information from the source control the project needs to include 
a package reference to the appropriate source control package. Microsoft provides source control packages for Git and TFVC managers:

 - Microsoft.Build.Tasks.Git
 - Microsoft.Build.Tasks.TFVC
  
These packages implement a protocol defined by MSBuild that allows extraction of necessary information from the source control system during build.
This protocol can be implemented by third party packages in order to support other source control systems.

Adding one of these packages to the project enables MSBuild to automatically detect the current source revision id (Git commit hash, TFS shelveset number),
the repository URL, source files that are not tracked by the source control and other useful information. 

Having this information available enables the following features:

1) Including source revision id in `AssemblyInformationalVersionAttribute` and in NuSpec of the package produced by the project.
2) Automatic detection and publishing of the repository URL.
3) Embedding sources to the PDB that are not tracked by source control.
4) Generating [Source Link](https://github.com/dotnet/core/blob/master/Documentation/diagnostics/source_link.md) that 
   enables debuggers to find sources when stepping through the DLL/EXE produced by the project.

To generate Source Link having just the source control package is not sufficient, since various source control providers (hosts) 
differ in the way how they expose the content of the hosted repositories. A package specific to the provider is needed. 
The following Source Link packages are currently available:

- Microsoft.SourceLink.GitHub (depends on Microsoft.Build.Tasks.Git package)
- Microsoft.SourceLink.VSTS.Git (depends on Microsoft.Build.Tasks.Git package)
- Microsoft.SourceLink.VSTS.TFVC (depends on Microsoft.Build.Tasks.TFVC package)

Each SourceLink package depends on the corresponding source control package. Referencing a SourceLink package makes the dependent source control package also referenced, 
thus providing the other source control features to the project.

Note that it is possible and supported to reference multiple SourceLink packages in a single project provided they depend on the same source control package.
This is necessary when the project sources are stored in mutliple submodules hosted by different providers (e.g. VSTS repository containing a GitHub submodule).

## Basic Settings

### ContinuousIntegrationBuild

Boolean that indicates that the build executes on a build/CI server. 

By default, the property is set to true on build servers that comply with
[Standard CI](https://github.com/dotnet/designs/blob/86f4ed0e39fc1b1ab5c4128990b17c4aead4420f/proposed/standard-ci-env-variables.md)
specification and set ```STANDARD_CI_REPOSITORY_URL``` variable.

```ContinuousIntegrationBuild``` variable is used within the build system to enable settings that only apply to official builds, as opposed to local builds on developer machine.

### PublishRepositoryUrl

The URL of the repository supplied by the CI server or retrieved from source control manager is stored in ```PrivateRepositoryUrl``` variable.
This value is not directly embedded in build outputs to avoid inadvertently publishing links to private repositories.
Instead, ```PublishRepositoryUrl``` needs to be set by the project in order to publish the URL into ```RepositoryUrl``` property,
which is used e.g. in the nuspec file generated for NuGet package produced by the project.

### DeterministicSourcePaths

By setting ```DeterministicSourcePaths``` to true the project opts into mapping all source paths included in the project outputs to deterministic values, 
i.e. values that do not depend on the exact location of the project sources on disk. 

Only set ```DeterministicSourcePaths``` to true on a build/CI server, never for for local builds.
In order for the debugger to find source files when debugging a locally built binary the PDB must contain original, unmapped local paths.

By default, ```DeterministicSourcePaths``` is set to true if both ```Deterministic``` and ```ContinuousIntegrationBuild``` are true.

### EmbedAllSources

Set ```EmbedAllSources``` to true to instruct the build system to embed all project source files into the generated PDB.

### EmbedUntrackedSources

Set ```EmbedUntrackedSources``` to true to instruct the build system to embed project source files that are not tracked by the source control or imported from a source package to the generated PDB.

Has no effect if ```EmbedAllSources``` is true.

## Example

The following project settings result in repository url and commit hash automatically detected and included in NuSpec, commit hash included in ```AssemblyInformationalVersionAttribute```, all source files available on GitHub linked via Source Link (including those in submodules) and source files not available on GitHub embedded in the PDB.

```xml
<Project Sdk="Microsoft.NET.Sdk">
 <PropertyGroup>
    <TargetFramework>netcoreapp1.0</TargetFramework>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="1.0.0" PrivateAssets="All"/>
  </ItemGroup>
</Project>
```

## Advanced Settings

### IncludeSourceRevisionInInformationalVersion

When true and a source control package is present the Source Revision id is included in the `AssemblyInformationalVersionAttribute`. 
True by default, set to false to suppress publishing Source Revision id to the attribute.

### SourceRevisionId

Set by target `SetSourceRevisionId` and consumed by NuGet `Pack` target and `GenerateAssemblyInfo` target. 
May be used by custom targets that need this information.

### EnableSourceLink

This property is implicitly set to true by a SourceLink package, unless it's explicitly set by the project to false.
Including a SourceLink package thus enables Source Link generation unless disabled by the project.

### SourceRoot

Item group that lists all source roots that the project source files reside under and their mapping to source control server URLs. This includes both source files under source control as well as source files in source packages.

Source root metadata:

- _Identity_: full path to the source root directory ending with a directory separator

All source control roots have the following metadata:

- _SourceControl_: the name of source control system, if the directory is a source source control root (e.g. `git`, `tfvc`, etc.)
- _RevisionId_: revision id (e.g. git commit hash)

Additional soruce-control specific metadata may be defined (depends on the source control system). 

For example, for Git:

- _RepositoryUrl_: e.g. ```http://github.com/dotnet/corefx```

For TFVC:

- _CollectionUrl_
- _ProjectId_
- _ServerPath_

Nested source control roots have the following metadata (e.g. submodules):

- _NestedRoot_: URL to the source root relative to the containing source root (e.g. ```src/submodules/mysubmodule```)
- _ContainingRoot_: the identity if the containing source root

Source roots not under source control:
- _SourceLinkUrl_: URL to use in source link mapping, including ```*``` wildcard (e.g. ```https://raw.githubusercontent.com/dotnet/roslyn/42abf2e6642db97d2314c017eb179075d5042028/src/Dependencies/CodeAnalysis.Debugging/*```)
