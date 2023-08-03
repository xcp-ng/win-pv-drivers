# Main build script
param(
	[Parameter(Mandatory = $true)]
	[string]$Type,
	[Parameter(Mandatory = $true)]
	[string]$ConfigurationBase,
	[switch]$Sdv,
    [Parameter(Mandatory = $true)]
    [string]$RepoName
)

Write-Host "Building PV driver:" $RepoName

#common
$visualstudioversion = $Env:VisualStudioVersion
if (!$Env:VisualStudioVersion) {
    Write-Host "VisualStudioVersion environment variable is not set"
    exit -1
}
$solutiondir = @{ "14.0" = "vs2015"; "15.0" = "vs2017"; "16.0" = "vs2019"; "17.0" = "vs2022"; }
$SolutionDir = $solutiondir[$visualstudioversion]

# Script Body
Function Build {
	param(
		[string]$Arch,
		[string]$Type,
		[string]$ConfigurationBase,
        [string]$RepoName
	)

	$params = @{
		SolutionDir = $SolutionDir;
		ConfigurationBase = $ConfigurationBase;
		Arch = $Arch;
		Type = $Type;
        RepoName = $RepoName;
		}
	& ".\msbuild.ps1" @params
	if ($LASTEXITCODE -ne 0) {
		Write-Host -ForegroundColor Red "ERROR: Build failed, code:" $LASTEXITCODE
		Exit $LASTEXITCODE
	}
}

if ($Type -ne "free" -and $Type -ne "checked") {
	Write-Host "Invalid Type"
	Exit -1
}

if ([string]::IsNullOrEmpty($Env:VENDOR_NAME)) {
	Set-Item -Path Env:VENDOR_NAME -Value 'Xen Project'
}

if ([string]::IsNullOrEmpty($Env:VENDOR_PREFIX)) {
	Set-Item -Path Env:VENDOR_PREFIX -Value 'XP'
}

if ([string]::IsNullOrEmpty($Env:PRODUCT_NAME)) {
	Set-Item -Path Env:PRODUCT_NAME -Value 'Xen'
}

if ([string]::IsNullOrEmpty($Env:BUILD_NUMBER)) {
	if (Test-Path ".build_number") {
		$BuildNum = Get-Content -Path ".build_number"
		Set-Content -Path ".build_number" -Value ([int]$BuildNum + 1)
	} else {
		$BuildNum = '0'
		Set-Content -Path ".build_number" -Value '1'
	}
	Set-Item -Path Env:BUILD_NUMBER -Value $BuildNum
}

Set-Item -Path Env:MAJOR_VERSION -Value '9'
Set-Item -Path Env:MINOR_VERSION -Value '1'
Set-Item -Path Env:MICRO_VERSION -Value '0'

if ($ConfigurationBase -eq "Windows 8") {
	Build "x86" $Type $ConfigurationBase
	Build "x64" $Type $ConfigurationBase
} else {
	Build "x64" $Type $ConfigurationBase
}

