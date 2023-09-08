#
# Main build script
#

param(
	[Parameter(Mandatory = $true)]
	[string]$RepoName,
	[Parameter(Mandatory = $true)]
	[string]$Type,
	[switch]$Sdv
)

#
# Script Body
#

$solutiondir = @{
	"14.0" = "vs2015";
	"15.0" = "vs2017";
	"16.0" = "vs2019";
	"17.0" = "vs2019";
	"EWDK" = "ewdk";
}

$configurationbase = @{
	"14.0" = "Windows 8";
	"15.0" = "Windows 8";
	"16.0" = "Windows 8";
	"17.0" = "Windows 10";
	"EWDK" = "Windows 10";
}

Function UnifiedBuild {
	param(
		[Parameter(Mandatory = $true)]
		[string]$RepoName,
		[string]$Arch = "x64",
		[string]$Type = "free",
		[string]$BuildType = "normal"
	)
	
	Write-Host "RepoName is: $RepoName"
	if (-Not (Test-Path -Path $RepoName)) {
		Write-Host "Path does not exist: $RepoName"
		Exit -1
	}
	
	$visualstudioversion = $Env:VisualStudioVersion
	$params = @{
		SolutionDir = $solutiondir[$visualstudioversion];
		ConfigurationBase = $configurationbase[$visualstudioversion];
		RepoName = $RepoName;
		Arch = $Arch;
		Type = $Type
	}
	
	if ($BuildType -eq "sdv") {
		$params["ConfigurationBase"] = "Windows 10"
		$params["Type"] = "sdv"
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

UnifiedBuild -RepoName $RepoName -Arch "x64" -Type $Type

if ($Sdv) {
	UnifiedBuild -RepoName $RepoName -BuildType "sdv"
}
