[CmdletBinding()]
param(
    [Parameter()][string]$Wix = "$Env:USERPROFILE\.nuget\packages\wixtoolset.sdk\7.0.1-xcpng.1\tools\net472\x64\wix.exe"
)

$ErrorActionPreference = 'Stop'

$Baseline = Resolve-Path "XenTools-x64.msi"
$Transform = Resolve-Path "XenTools-fix-9.2.351.mst"
$Update = "XenTools-fix-9.2.351.msi"

if ((Get-FileHash $Baseline -Algorithm SHA256).Hash -ine "4E77F6B1BF79D064603349E0AD6AC5CC204FC5910A4C44A6A052AFA2973EED35") {
    throw "Baseline file doesn't match"
}

# just a new random guid for deterministic patch generation
$PackageCode = "{4F7DEEB5-F1E9-4875-8B31-D038EF137D08}"

Copy-Item $Baseline -Destination $Update -Force
$ResolvedUpdate = Resolve-Path $Update
msitran.exe -a $Transform $ResolvedUpdate
if ($LASTEXITCODE -ne 0) {
    throw "msitran failed with exit code $LASTEXITCODE"
}
msiinfo.exe $ResolvedUpdate -v $PackageCode
if ($LASTEXITCODE -ne 0) {
    throw "msiinfo failed with exit code $LASTEXITCODE"
}
& $Wix build Patch.wxs -o "XenTools-fix-9.2.351.msp"
