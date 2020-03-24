# Standardized Environment Variables for CI Services

The .NET Core SDK is implementing new scenarios that require source control manager information. We want to make it very easy to embed repo-specific information in built assets as a means of creating a strong relationship between binary and source.

To make it possible to provide application platform provided experiences across CI services, we need a standardized set of environment variables that are supported across those same CI services. Ideally, this set of environment variables would be supported across multiple CI services and useful for multiple application environments, not just .NET.

## Context

The .NET Core SDK Team is implementing a set of scenarios that establish a strong relationship between binary and source. Some of these scenarios are purely for auditing/pedigree analysis and others are for debugging.

The more simple scenarios simply record source repo information that is readily available into various informational fields in built assets.

Source control information needed:

* Commit ID/hash
* Repository URL
* Source Control Manager name (`git`, `tfvc`, etc.)

Places to store this information:

* AssemblyInformationalVersion attribute.
* NuGet nuspec file (manifest for NuGet package).

This information doesn't need to be in a specific format, although accurate would be good. It should be descriptive and intuitive per the given source control system so that human readers can work their way back to source (assuming they have access).

The following examples demonstrate how the Roslyn team has implemented this same scenario in their own build. It should be easy for anyone to include the same information in built artifacts.

Example -- Microsoft.CodeAnalysis.CSharp.dll:

```csharp
[assembly: AssemblyInformationalVersion("2.4.0-beta1-62122-04+ab56a4a6c32268d925014a3e45ddce61fba715cd")]
```

Example -- Microsoft.Net.Compilers.nupkg (NuGet package):

```xml
<repository type="git" url="https://github.com/dotnet/roslyn" commit="ab56a4a6c32268d925014a3e45ddce61fba715cd"/>
```

Note that the final implementation provided by the .NET Core SDK might look slightly differently from the Roslyn examples, but it will be similar. There will be a separate spec on this feature. This document is focussed on the CI service integration.

## Proposed Standard Environment Variables

The .NET Core SDK needs are oriented around source control. As a result, the initial list is source control oriented, but there is no affinity to source control on the general idea of standardized environment variables.

It is important that these environment variables do not conflict with other variables. To avoid that, all environment variables will be prepended with `STANDARD_CI_`. This name is a first proposal for the idea and it may get changed based on feedback.

* **STANDARD\_CI\_SOURCE\_REVISION\_ID** -- Commit hash / ID; Example: 2ba93796dcf132de447886d4d634414ee8cb069d
* **STANDARD\_CI\_REPOSITORY\_URL** -- URL for repository; Example: https://github.com/dotnet/corefx
* **STANDARD\_CI\_REPOSITORY\_TYPE** -- Source control manager name; Example: `git`, `TFVC`, `mercurial`, `svn`

The following strings are well-known values for `STANDARD_CI_REPOSITORY_TYPE`. Other values can be used for source control managers not listed in this table.

|  `STANDARD_CI_REPOSITORY_TYPE` | Source Control Manager |
| ------------------------------ | ---------------------- |
| `git`                          | [git](https://git-scm.com) |
| `tfvc`                         | [Team Foundation Version Control](https://docs.microsoft.com/en-us/vsts/tfvc) |
| `svn`                          | [Apache Subversion](https://subversion.apache.org) |
| `mercurial`                    | [Mercurial](https://www.mercurial-scm.org) |

## Support from CI Services

This plan will only work if CI services decide to support these environment variables. An important question is whether CI services have similar environment variables already. The table below suggests that the information we need is already available. An arbitrary sample of CI services were picked for this exercise.

| Environment Variable | VSTS | Travis CI| AppVeyor | Circle CI | AWS CodeBuild | Team City | OpenShift |
| -------------------- | ---- | -------- | -------- | --------- | ------------- | --------- | --------- |
|STANDARD\_CI\_SOURCE\_REVISION\_ID | BUILD\_SOURCEVERSION | TRAVIS\_COMMIT |APPVEYOR\_REPO\_COMMIT | CIRCLE\_SHA1 | CODEBUILD\_RESOLVED\_SOURCE\_VERSION | build.vcs.number | OPENSHIFT\_BUILD\_COMMIT |
|STANDARD\_CI\_REPOSITORY\_URL|BUILD\_REPOSITORY\_URI| | | CIRCLE\_REPOSITORY\_URL | CODEBUILD\_SOURCE\_REPO\_URL | vcsroot.url | OPENSHIFT\_BUILD\_SOURCE |
|STANDARD\_CI\_REPOSITORY\_TYPE |  | |APPVEYOR\_REPO\_SCM |  | 

The [VSTS](https://www.visualstudio.com/team-services/) team has graciously agreed to publish environment variables in the proposed STANDARD\_CI format.

* [VSTS](https://www.visualstudio.com/team-services/) -- [environment variables](https://docs.microsoft.com/en-us/vsts/build-release/concepts/definitions/build/variables?tabs=batch#predefined-variables)
* [Travis CI](https://travis-ci.org/) -- [environment variables](https://docs.travis-ci.com/user/environment-variables/#Default-Environment-Variables)
* [AppVeyor](https://www.appveyor.com/) -- [environment variables](https://www.appveyor.com/docs/environment-variables/)
* [Circle CI](https://circleci.com) -- [environment variables](https://circleci.com/docs/2.0/env-vars)
* [AWS CodeBuild](https://aws.amazon.com/codebuild/) -- [environment variables](http://docs.aws.amazon.com/codebuild/latest/userguide/build-env-ref-env-vars.html)
* [Team City](https://confluence.jetbrains.com/display/TCDL/Predefined+Build+Parameters)
* [OpenShift](https://docs.openshift.com/enterprise/3.1/dev_guide/builds.html#output-image-environment-variables)
