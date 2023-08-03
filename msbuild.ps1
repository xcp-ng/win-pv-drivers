# Wrapper script for MSBuild
param(
	[string]$SolutionDir = "vs2019",
	[string]$ConfigurationBase = "Windows 10",
	[Parameter(Mandatory = $true)]
	[string]$Arch,
	[Parameter(Mandatory = $true)]
	[string]$Type,
    [Parameter(Mandatory = $true)]
    [string]$RepoName
)


Function Run-MSBuild {
	param(
		[string]$SolutionPath,
		[string]$Name,
		[string]$Configuration,
		[string]$Platform,
		[string]$Target = "Build",
		[string]$Inputs = ""
	)

	$c = "msbuild.exe"
	$c += " /m:4"
	$c += [string]::Format(" /p:Configuration=""{0}""", $Configuration)
	$c += [string]::Format(" /p:Platform=""{0}""", $Platform)
	$c += [string]::Format(" /t:""{0}"" ", $Target)
	if ($Inputs) {
		$c += [string]::Format(" /p:Inputs=""{0}"" ", $Inputs)
	}
	$c += '"' + (Join-Path -Path $SolutionPath -ChildPath $Name) + '"'

	Invoke-Expression $c
	if ($LASTEXITCODE -ne 0) {
		Write-Host -ForegroundColor Red "ERROR: MSBuild failed, code:" $LASTEXITCODE
		Exit $LASTEXITCODE
	}
}

Function Run-MSBuildSDV {
	param(
		[string]$SolutionPath,
		[string]$Name,
		[string]$Configuration,
		[string]$Platform
	)

	$basepath = Get-Location
	$versionpath = Join-Path -Path $SolutionPath -ChildPath "version"
	$projpath = Join-Path -Path $SolutionPath -ChildPath $Name
	Set-Location $projpath

	$project = [string]::Format("{0}.vcxproj", $Name)
	Run-MSBuild $versionpath "version.vcxproj" $Configuration $Platform "Build"
	Run-MSBuild $projpath $project $Configuration $Platform "Build"
	Run-MSBuild $projpath $project $Configuration $Platform "sdv" "/clean"
	Run-MSBuild $projpath $project $Configuration $Platform "sdv" "/check:default.sdv /debug"
	Run-MSBuild $projpath $project $Configuration $Platform "dvl"

	$refine = Join-Path -Path $projpath -ChildPath "refine.sdv"
	if (Test-Path -Path $refine -PathType Leaf) {
		Run-MSBuild $projpath $project $Configuration $Platform "sdv" "/refine"
	}

	Copy-Item "*DVL*" -Destination $SolutionPath

	Set-Location $basepath
}

#
# Script Body
#

$configuration = @{ "free" = "$ConfigurationBase Release"; "checked" = "$ConfigurationBase Debug"; "sdv" = "$ConfigurationBase Release"; }
$platform = @{ "x86" = "Win32"; "x64" = "x64" }
$solutionpath = Resolve-Path "$RepoName\$SolutionDir"

Set-ExecutionPolicy -Scope CurrentUser -Force Bypass

if ($Type -eq "sdv") {
	$archivepath = $RepoName

	if (-Not (Test-Path -Path $archivepath)) {
		New-Item -Name $archivepath -ItemType Directory | Out-Null
	}

	Run-MSBuildSDV $solutionpath $RepoName $configuration["sdv"] $platform[$Arch]

	Copy-Item -Path (Join-Path -Path $SolutionPath -ChildPath "*DVL*") -Destination $archivepath
}
else {
	Run-MSBuild $solutionpath "$RepoName.sln" $configuration[$Type] $platform[$Arch]
}
