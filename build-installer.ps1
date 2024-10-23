[CmdletBinding()]
param (
    [Parameter()]
    [string]$Target = "Rebuild",
    [Parameter(Mandatory)]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [string]$Platform,
    [Parameter()]
    [string]$OutDir = "$PSScriptRoot\package"
)

$ErrorActionPreference = "Stop"

msbuild.exe "$PSScriptRoot\installer\installer.sln" /t:$Target /p:Configuration=$Configuration /p:Platform=$Platform
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with error $LASTEXITCODE"
}

Remove-Item -Path $OutDir -Force -Recurse -ErrorAction SilentlyContinue
if ($Target -ine "Clean") {
    New-Item -Path $OutDir -ItemType Directory -Force
    Copy-Item -Path "$PSScriptRoot\installer\bin\$Platform\$Configuration\en-US\*" -Destination $OutDir\

    $XenCleanOutDir = "$OutDir\XenClean\$Platform"
    New-Item -Path $XenCleanOutDir -ItemType Directory -Force
    Copy-Item -Path "$PSScriptRoot\XenClean\bin\$Platform\$Configuration\net462\Invoke-XenClean.ps1" -Destination $XenCleanOutDir\

    New-Item -Path $XenCleanOutDir\bin -ItemType Directory -Force
    Copy-Item -Path "$PSScriptRoot\XenClean\bin\$Platform\$Configuration\net462\*" -Exclude Invoke-XenClean.ps1 -Destination $XenCleanOutDir\bin\
}
