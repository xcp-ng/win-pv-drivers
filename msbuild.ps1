# Wrapper script for MSBuild
param(
	[string]$SolutionDir = "vs2019",
	[string]$ConfigurationBase = "Windows 10",
	[Parameter(Mandatory = $true)]
	[string]$RepoName,
	[Parameter(Mandatory = $true)]
	[string]$Arch,
	[Parameter(Mandatory = $true)]
	[string]$Type,
	[string[]]$ProjectNames = @("xencrsh", "xendisk", "xenvbd")
)

$SolutionName = $RepoName -replace 'win-', ''

# Function to run MSBuild with specified parameters
Function Run-MSBuild {
	param(
		[string]$SolutionPath,
		[string]$Name,
		[string]$Configuration,
		[string]$Platform,
		[string]$Target = "Build",
		[string]$Inputs = ""
	)

	# Construct options in a structured manner
	$options = @(
		"/m:4",
		"/p:Configuration=`"$Configuration`"",
		"/p:Platform=`"$Platform`"",
		"/t:`"$Target`""
	)

	if ($Inputs) {
		$options += "/p:Inputs=`"$Inputs`""
	}

	$options += ('"{0}"' -f (Join-Path -Path $SolutionPath -ChildPath $Name))

	# Execute MSBuild with the options
	Invoke-Expression -Command ("msbuild.exe " + [string]::Join(" ", $options))

	if ($LASTEXITCODE -ne 0) {
		Write-Host -ForegroundColor Red "ERROR: MSBuild failed, code:" $LASTEXITCODE
		Exit $LASTEXITCODE
	}
}

# Function to run MSBuild for SDV analysis with specific parameters
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

# Main script body
$configuration = @{
	"free" = "$ConfigurationBase Release";
	"checked" = "$ConfigurationBase Debug";
	"sdv" = "$ConfigurationBase Release"
}
$platform = @{ "x86" = "Win32"; "x64" = "x64" }

Set-Location -Path $RepoName
$solutionpath = Resolve-Path $SolutionDir

Set-ExecutionPolicy -Scope CurrentUser -Force Bypass

if ($Type -eq "free" -or $Type -eq "checked") {
	Run-MSBuild $solutionpath "$SolutionName.sln" $configuration[$Type] $platform[$Arch]
}
elseif ($Type -eq "sdv") {
	if (-Not (Test-Path -Path $SolutionName)) {
		New-Item -Name $SolutionName -ItemType Directory | Out-Null
	}
	if (-not $ProjectNames) {
		$ProjectNames = @($SolutionName)
	}
	foreach ($ProjectName in $ProjectNames) {
		Run-MSBuildSDV $solutionpath $ProjectName $configuration["sdv"] $platform[$Arch]
	}

	Copy-Item -Path (Join-Path -Path $solutionPath -ChildPath "*DVL*") -Destination $SolutionName
}
