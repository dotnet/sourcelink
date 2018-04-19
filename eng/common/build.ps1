[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = "Debug",
  [string] $solution = "",
  [string] $verbosity = "minimal",
  [switch] $restore,
  [switch] $deployDeps,
  [switch] $build,
  [switch] $rebuild,
  [switch] $deploy,
  [switch] $test,
  [switch] $integrationTest,
  [switch] $sign,
  [switch] $pack,
  [switch] $ci,
  [switch] $prepareMachine,
  [switch] $help,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

set-strictmode -version 2.0
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Print-Usage() {
    Write-Host "Common settings:"
    Write-Host "  -configuration <value>  Build configuration Debug, Release"
    Write-Host "  -verbosity <value>      Msbuild verbosity (q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic])"
    Write-Host "  -help                   Print help and exit"
    Write-Host ""

    Write-Host "Actions:"
    Write-Host "  -restore                Restore dependencies"
    Write-Host "  -build                  Build solution"
    Write-Host "  -rebuild                Rebuild solution"
    Write-Host "  -deploy                 Deploy built VSIXes"
    Write-Host "  -deployDeps             Deploy dependencies (e.g. VSIXes for integration tests)"
    Write-Host "  -test                   Run all unit tests in the solution"
    Write-Host "  -integrationTest        Run all integration tests in the solution"
    Write-Host "  -sign                   Sign build outputs"
    Write-Host "  -pack                   Package build outputs into NuGet packages and Willow components"
    Write-Host ""

    Write-Host "Advanced settings:"
    Write-Host "  -solution <value>       Path to solution to build"
    Write-Host "  -ci                     Set when running on CI server"
    Write-Host "  -prepareMachine         Prepare machine for CI run"
    Write-Host ""
    Write-Host "Command line arguments not listed above are passed thru to msbuild."
    Write-Host "The above arguments can be shortened as much as to be unambiguous (e.g. -co for configuration, -t for test, etc.)."
}

if ($help -or (($properties -ne $null) -and ($properties.Contains("/help") -or $properties.Contains("/?")))) {
  Print-Usage
  exit 0
}

function Create-Directory([string[]] $path) {
  if (!(Test-Path $path)) {
    New-Item -path $path -force -itemType "Directory" | Out-Null
  }
}

function InitializeDotNetCli {
  # Don't resolve runtime, shared framework, or SDK from other locations to ensure build determinism
  $env:DOTNET_MULTILEVEL_LOOKUP=0
  
  # Disable first run since we do not need all ASP.NET packages restored.
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Source Build uses DotNetCoreSdkDir variable
  if ($env:DotNetCoreSdkDir -ne $null) {
    $env:DOTNET_INSTALL_DIR = $env:DotNetCoreSdkDir    
  }

  # Use dotnet installation specified in DOTNET_INSTALL_DIR if it contains the required SDK version, 
  # otherwise install the dotnet CLI and SDK to repo local .dotnet directory to avoid potential permission issues.
  if (($env:DOTNET_INSTALL_DIR -ne $null) -and (Test-Path(Join-Path $env:DOTNET_INSTALL_DIR "sdk\$($GlobalJson.sdk.version)"))) {
    $dotnetRoot = $env:DOTNET_INSTALL_DIR
  } else {
    $dotnetRoot = Join-Path $RepoRoot ".dotnet"
    $env:DOTNET_INSTALL_DIR = $dotnetRoot
    
    if ($restore) {
      InstallDotNetCli $dotnetRoot
    }
  }

  $global:BuildDriver = Join-Path $dotnetRoot "dotnet.exe"    
  $global:BuildArgs = "msbuild"
}

function InstallDotNetCli([string] $dotnetRoot) {
  $installScript = "$dotnetRoot\dotnet-install.ps1"
  if (!(Test-Path $installScript)) { 
    Create-Directory $dotnetRoot
    Invoke-WebRequest "https://dot.net/v1/dotnet-install.ps1" -OutFile $installScript
  }
  
  & $installScript -Version $GlobalJson.sdk.version -InstallDir $dotnetRoot
  if ($lastExitCode -ne 0) {
    throw "Failed to install dotnet cli (exit code '$lastExitCode')."
  }
}

function InitializeVisualStudioBuild {
  $inVSEnvironment = !($env:VS150COMNTOOLS -eq $null) -and (Test-Path $env:VS150COMNTOOLS)

  if ($inVSEnvironment) {
    $vsInstallDir = Join-Path $env:VS150COMNTOOLS "..\.."
  } else {    
    $vsInstallDir = LocateVisualStudio
  
    $env:VS150COMNTOOLS = Join-Path $vsInstallDir "Common7\Tools\"
    $env:VSSDK150Install = Join-Path $vsInstallDir "VSSDK\"
    $env:VSSDKInstall = Join-Path $vsInstallDir "VSSDK\"
  }

  $global:BuildDriver = Join-Path $vsInstallDir "MSBuild\15.0\Bin\msbuild.exe"
  $global:BuildArgs = "/nodeReuse:$(!$ci)"
}

function LocateVisualStudio {
  $vswhereVersion = $GlobalJson.vswhere.version
  $toolsRoot = Join-Path $RepoRoot ".tools"
  $vsWhereDir = Join-Path $toolsRoot "vswhere\$vswhereVersion"
  $vsWhereExe = Join-Path $vsWhereDir "vswhere.exe"

  if (!(Test-Path $vsWhereExe)) {
    Create-Directory $vsWhereDir
    Write-Host "Downloading vswhere"
    Invoke-WebRequest "https://github.com/Microsoft/vswhere/releases/download/$vswhereVersion/vswhere.exe" -OutFile $vswhereExe
  }

  $vsInstallDir = & $vsWhereExe -latest -prerelease -property installationPath -requires Microsoft.Component.MSBuild -requires Microsoft.VisualStudio.Component.VSSDK -requires Microsoft.Net.Component.4.6.TargetingPack -requires Microsoft.VisualStudio.Component.Roslyn.Compiler -requires Microsoft.VisualStudio.Component.VSSDK

  if (!(Test-Path $vsInstallDir)) {
    throw "Failed to locate Visual Studio (exit code '$lastExitCode')."
  }

  return $vsInstallDir
}

function InitializeToolset {
  $toolsetVersion = $GlobalJson.'msbuild-sdks'.'RoslynTools.RepoToolset'
  $toolsetLocationFile = Join-Path $ToolsetDir "$toolsetVersion.txt"

  if (Test-Path $toolsetLocationFile) {
    $path = Get-Content $toolsetLocationFile
    if (Test-Path $path) {
      $global:ToolsetBuildProj = $path
      return
    }
  }
    
  if (-not $restore) {
    throw "Toolset version $toolsetVersion has not been restored."
  }

  $proj = Join-Path $ToolsetDir "restore.proj"  

  '<Project Sdk="RoslynTools.RepoToolset"/>' | Set-Content $proj
  & $BuildDriver $BuildArgs $proj /t:__WriteToolsetLocation /m /nologo /clp:None /warnaserror /bl:$ToolsetRestoreLog /v:$verbosity /p:__ToolsetLocationOutputFile=$toolsetLocationFile
    
  if ($lastExitCode -ne 0) {
    throw "Failed to restore toolset (exit code '$lastExitCode'). See log: $ToolsetRestoreLog"
  }

 $global:ToolsetBuildProj = Get-Content $toolsetLocationFile
}

function Build {
  & $BuildDriver $BuildArgs $ToolsetBuildProj /m /nologo /clp:Summary /warnaserror /v:$verbosity /bl:$Log /p:Configuration=$configuration /p:Projects=$solution /p:RepoRoot=$RepoRoot /p:Restore=$restore /p:DeployDeps=$deployDeps /p:Build=$build /p:Rebuild=$rebuild /p:Deploy=$deploy /p:Test=$test /p:IntegrationTest=$integrationTest /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci $properties
}

function Stop-Processes() {
  Write-Host "Killing running build processes..."
  Get-Process -Name "msbuild" -ErrorAction SilentlyContinue | Stop-Process
  Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process
  Get-Process -Name "vbcscompiler" -ErrorAction SilentlyContinue | Stop-Process
}

try {
  $RepoRoot = Join-Path $PSScriptRoot "..\.."
  $ArtifactsDir = Join-Path $RepoRoot "artifacts"
  $ToolsetDir = Join-Path $ArtifactsDir "toolset"
  $LogDir = Join-Path (Join-Path $ArtifactsDir $configuration) "log"
  $Log = Join-Path $LogDir "Build.binlog"
  $ToolsetRestoreLog = Join-Path $LogDir "ToolsetRestore.binlog"
  $TempDir = Join-Path (Join-Path $ArtifactsDir $configuration) "tmp"
  $GlobalJson = Get-Content(Join-Path $RepoRoot "global.json") | ConvertFrom-Json
  
  if ($solution -eq "") {
    $solution = Join-Path $RepoRoot "*.sln"
  }

  if ($env:NUGET_PACKAGES -eq $null) {
    # Use local cache on CI to ensure deterministic build,
    # use global cache in dev builds to avoid cost of downloading packages.
    $env:NUGET_PACKAGES = if ($ci) { Join-Path $RepoRoot ".packages" } 
                          else { Join-Path $env:UserProfile ".nuget\packages" }
  }

  Create-Directory $ToolsetDir
  Create-Directory $LogDir
  
  if ($ci) {
    Create-Directory $TempDir
    $env:TEMP = $TempDir
    $env:TMP = $TempDir
  }
    
  # Presence of vswhere.version indicates the repo needs to build using VS msbuild
  if ((Get-Member -InputObject $GlobalJson -Name "vswhere") -ne $null) {    
    InitializeVisualStudioBuild
  } elseif ((Get-Member -InputObject $GlobalJson -Name "sdk") -ne $null) {  
    InitializeDotNetCli
  } else {
    throw "/global.json must either specify 'sdk.version' or 'vswhere.version'."
  }

  if ($ci) {
    Write-Host "Using $BuildDriver"
  }

  InitializeToolset

  Build
  exit $lastExitCode
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  exit 1
}
finally {
  Pop-Location
  if ($ci -and $prepareMachine) {
    Stop-Processes
  }
}

