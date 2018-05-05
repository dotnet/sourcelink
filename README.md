# SourceLink

SourceLink is a language- and source-control agnostic system for providing first-class source debugging experiences for binaries. The primary goal of the project is to enable anyone building NuGet libraries to provide source debugging for their users with almost no effort. Microsoft libraries, such as .NET Core and Roslyn have enabled SourceLink. SourceLink is supported by Microsoft.

SourceLink is a [set of packages](https://dotnet.myget.org/Gallery/sourcelink) and a [specification](https://github.com/dotnet/designs/blob/master/accepted/diagnostics/source-link.md#source-link-file-specification) for describing source control metadata that can be embedded in symbols, binaries and packages.

Visual Studio 15.3+ supports SourceLink, reading SourceLink information from symbols while debugging. It downloads and displays the appropriate commit-specific source for users, such as from [raw.githubusercontent](https://raw.githubusercontent.com/dotnet/roslyn/681cbc414542ffb9fb13ded613d26a88ea73a44b/src/VisualStudio/Core/Def/Implementation/ProjectSystem/AbstractProject.cs), enabling breakpoints and all other sources debugging experience on arbitrary NuGet dependencies.

The [original SourceLink implementation](https://github.com/ctaggart/SourceLink) was provided by [@ctaggart](https://github.com/ctaggart). Thanks! The .NET Team and Cameron worked together to make this implementation available in the .NET Foundation.

## Using SourceLink

You can enable SourceLink in your own project by following this [example](https://github.com/dotnet/sourcelink/blob/master/docs/Readme.md#example):

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

## Builds

Pre-release builds are available on MyGet gallery: https://dotnet.myget.org/Gallery/sourcelink.

[//]: # (Begin current test results)

|    | x64 Debug|x64 Release|
|:--:|:--:|:--:|
|**Windows**|[![Build Status](https://ci2.dot.net/job/Private/job/dotnet_sourcelink/job/master/job/Windows_NT_Debug/badge/icon)](https://ci2.dot.net/job/Private/job/dotnet_sourcelink/job/master/job/Windows_NT_Debug/)|[![Build Status](https://ci2.dot.net/job/Private/job/dotnet_sourcelink/job/master/job/Windows_NT_Release/badge/icon)](https://ci2.dot.net/job/Private/job/dotnet_sourcelink/job/master/job/Windows_NT_Release/)|
|**Ubuntu 16.04**|[![Build Status](https://ci2.dot.net/job/Private/job/dotnet_sourcelink/job/master/job/Ubuntu16.04_Debug/badge/icon)](https://ci2.dot.net/job/Private/job/dotnet_sourcelink/job/master/job/Ubuntu16.04_Debug/)|[![Build Status](https://ci2.dot.net/job/Private/job/dotnet_sourcelink/job/master/job/Ubuntu16.04_Release/badge/icon)](https://ci2.dot.net/job/Private/job/dotnet_sourcelink/job/master/job/Ubuntu16.04_Release/)|

[//]: # (End current test results)

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
