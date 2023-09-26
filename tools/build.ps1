#
# Main build script
#

param(
	[Parameter(Mandatory = $true)]
	[string]$Type,
	[string]$Arch,
	[string]$SignMode = "TestSign",
	[Parameter(Mandatory = $true)]
	[string]$DriverName
)

#
# Script Body
#

$visualstudioversion = $Env:VisualStudioVersion
$solutiondir = @{ "14.0" = "vs2015"; "15.0" = "vs2017"; "16.0" = "vs2019"; "17.0" = "vs2022"; }
$configurationbase = @{ "14.0" = "Windows 8"; "15.0" = "Windows 8"; "16.0" = "Windows 8"; "17.0" = "Windows 10"; }
$toolsDir = "$PSScriptRoot"

Function Build {
	param(
		[string]$Arch,
		[string]$Type
	)
	
	if ($Type -eq "codeql" -or $Type -eq "sdv") {
		$ConfigBase = "Windows 10"
	} else {
		if ($Type -ne "free" -and $Type -ne "checked") {
			Write-Host "Invalid Type"
			Exit -1
		}
		$ConfigBase = $configurationbase[$visualstudioversion]
	}

	$params = @{
		SolutionDir = $solutiondir[$visualstudioversion];
		ConfigurationBase = $ConfigBase;
		Arch = $Arch;
		Type = $Type;
		SignMode = $SignMode;
		DriverName = $DriverName
		}
	& "$toolsDir\msbuild.ps1" @params
	if ($LASTEXITCODE -ne 0) {
		Write-Host -ForegroundColor Red "ERROR: Build failed, code:" $LASTEXITCODE
		Exit $LASTEXITCODE
	}
}

if ([string]::IsNullOrEmpty($Arch)) {
	if ($Env:VisualStudioVersion -eq "17.0") {
		Build "x64" $Type
	}
	else {
		Build "x86" $Type
	}
}
else {
	Build $Arch $Type
}
