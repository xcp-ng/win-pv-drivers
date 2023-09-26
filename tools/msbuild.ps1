#
# Wrapper script for MSBuild
#
param(
	[string]$SolutionDir = "vs2019",
	[string]$ConfigurationBase = "Windows 10",
	[Parameter(Mandatory = $true)]
	[string]$Arch,
	[Parameter(Mandatory = $true)]
	[string]$Type,
	[string]$SignMode = "TestSign",
	[Parameter(Mandatory = $true)]
	[string]$DriverName
)

Function Run-MSBuild {
	param(
		[string]$SolutionPath,
		[string]$Name,
		[string]$Configuration,
		[string]$Platform,
		[string]$Target = "Build",
		[string]$Inputs = "",
		[switch]$CodeAnalysis
	)

	$c = "msbuild.exe"
	$c += " /m:4"
	$c += [string]::Format(" /p:Configuration=""{0}""", $Configuration)
	$c += [string]::Format(" /p:Platform=""{0}""", $Platform)
	$c += [string]::Format(" /p:SignMode=""{0}""", $SignMode)
	$c += [string]::Format(" /t:""{0}"" ", $Target)
	if ($Inputs) {
		$c += [string]::Format(" /p:Inputs=""{0}"" ", $Inputs)
	}
	if ($CodeAnalysis) {
		$c += "/p:RunCodeAnalysis=true "
		$c += "/p:EnablePREFast=true "
	}

	$c += Join-Path -Path $SolutionPath -ChildPath $Name

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
	Run-MSBuild $projpath $project $Configuration $Platform "Build" -CodeAnalysis
	Run-MSBuild $projpath $project $Configuration $Platform "dvl"

	$refine = Join-Path -Path $projpath -ChildPath "refine.sdv"
	if (Test-Path -Path $refine -PathType Leaf) {
		Run-MSBuild $projpath $project $Configuration $Platform "sdv" "/refine"
	}

	Copy-Item "*DVL*" -Destination $SolutionPath

	Set-Location $basepath
}

Function Run-CodeQL {
	param(
		[string]$SolutionPath,
		[string]$Name,
		[string]$Configuration,
		[string]$Platform,
		[string]$SearchPath
	)

	$projpath = Resolve-Path (Join-Path $SolutionPath $Name)
	$project = [string]::Format("{0}.vcxproj", $Name)
	$output = [string]::Format("{0}.sarif", $Name)
	$database = Join-Path "database" $Name
	$sarif = Join-Path $projpath $output

	# write a bat file to wrap msbuild parameters
	$bat = [string]::Format("{0}.bat", $Name)
	if (Test-Path $bat) {
		Remove-Item $bat
	}
	$a = "msbuild.exe"
	$a += " /m:4"
	$a += " /t:Rebuild"
	$a += [string]::Format(" /p:Configuration=""{0}""", $Configuration)
	$a += [string]::Format(" /p:Platform=""{0}""", $Platform)
	$a += [string]::Format(" /p:SignMode=""{0}""", $SignMode)
	$a += " "
	$a += Join-Path $projpath $project
	$a | Set-Content $bat

	# generate the database
	$b = "codeql"
	$b += " database"
	$b += " create"
	$b += " -l=cpp"
	$b += " -s=src"
	$b += " -c"
	$b += ' "' + (Resolve-Path $bat) + '" '
	$b += $database
	Invoke-Expression $b
	if ($LASTEXITCODE -ne 0) {
		Write-Host -ForegroundColor Red "ERROR: CodeQL failed, code:" $LASTEXITCODE
		Exit $LASTEXITCODE
	}
	Remove-Item $bat

	# perform the analysis on the database
	$c = "codeql"
	$c += " database"
	$c += " analyze "
	$c += $database
	$c += " windows_driver_recommended.qls"
	$c += " --format=sarifv2.1.0"
	$c += " --output="
	$c += $sarif
	$c += " --search-path="
	$c += $SearchPath

	Invoke-Expression $c
	if ($LASTEXITCODE -ne 0) {
		Write-Host -ForegroundColor Red "ERROR: CodeQL failed, code:" $LASTEXITCODE
		Exit $LASTEXITCODE
	}

	Copy-Item $sarif -Destination $SolutionPath
}

#
# Script Body
#

$configuration = @{ "free" = "$ConfigurationBase Release"; "checked" = "$ConfigurationBase Debug"; "sdv" = "$ConfigurationBase Release"; "codeql" = "$ConfigurationBase Release"; }
$platform = @{ "x86" = "Win32"; "x64" = "x64" }

# Read the driver-project mapping from JSON file
$toolsDir = "$PSScriptRoot"
$configFilePath = Join-Path -Path $toolsDir -ChildPath "drivers_config.json"
$jsonContent = Get-Content -Path "drivers_config.json" | ConvertFrom-Json
$projectlist = $jsonContent.$DriverName

if ($null -eq $projectlist) {
	Write-Host -ForegroundColor Red "ERROR: Invalid DriverName specified"
	Exit -1
}

$parentDir = [System.IO.Path]::GetDirectoryName((Get-Location).Path)
$solutionpath = Resolve-Path (Join-Path -Path $parentDir -ChildPath "$DriverName\$SolutionDir")
$solutionName = $DriverName -replace "^win-", "" # Remove "win-" prefix

Set-ExecutionPolicy -Scope CurrentUser -Force Bypass

if ($Type -eq "free" -or $Type -eq "checked") {
	Run-MSBuild $solutionpath "$solutionName.sln" $configuration[$Type] $platform[$Arch]
}
elseif ($Type -eq "codeql") {
	if (-Not (Test-Path -Path $DriverName)) {
		New-Item -Name $DriverName -ItemType Directory | Out-Null
	}

	if ([string]::IsNullOrEmpty($Env:CODEQL_QUERY_SUITE)) {
		$searchpath = Resolve-Path ".."
	} else {
		$searchpath = $Env:CODEQL_QUERY_SUITE
	}

	if (Test-Path "database") {
		Remove-Item -Recurse -Force "database"
	}
	New-Item -ItemType Directory "database" | Out-Null

	$projectlist | ForEach {
		Run-CodeQL $solutionpath $_ $configuration["codeql"] $platform[$Arch] $searchpath
	}

	Copy-Item -Path (Join-Path -Path $SolutionPath -ChildPath "*.sarif") -Destination $DriverName
}
elseif ($Type -eq "sdv") {
	if (-Not (Test-Path -Path $DriverName)) {
		New-Item -Name $DriverName -ItemType Directory | Out-Null
	}

	$projectlist | ForEach {
		Run-MSBuildSDV $solutionpath $_ $configuration["sdv"] $platform[$Arch]
	}

	Copy-Item -Path (Join-Path -Path $SolutionPath -ChildPath "*DVL*") -Destination $DriverName
}
