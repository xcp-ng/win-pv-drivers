#
# Generate version.h and inf file for driver
#

# Copy $InFileName -> $OutFileName replacing $Token$_.Key$Token with $_.Value from
# $Replacements
Function Copy-FileWithReplacements {
	param(
		[Parameter(Mandatory = $true)]
		[string]$InFileName,
		[Parameter(Mandatory = $true)]
		[string]$OutFileName,
		[hashtable]$Replacements,
		[string]$Token = "@"
	)

	Write-Host "Copy-FileWithReplacements"
	Write-Host $InFileName" -> "$OutFileName

	(Get-Content $InFileName) |
	ForEach-Object {
		$line = $_
		$Replacements.GetEnumerator() | ForEach-Object {
			$key = [string]::Format("{0}{1}{2}", $Token, $_.Name, $Token)
			if (([string]::IsNullOrEmpty($_.Value)) -and ($line.Contains($key))) {
				Write-Host "Skipping Line Containing " $_.Name
				$line = $null
			}
			$line = $line -replace $key, $_.Value
		}
		$line
	} |
	Set-Content $OutFileName
}

#
# Script Body
#
$TheYear = [int](Get-Date -UFormat "%Y")
$TheMonth = [int](Get-Date -UFormat "%m")
$TheDay = [int](Get-Date -UFormat "%d")
$InfArch = @{ "Win32" = "x86"; "x64" = "amd64" }
$InfDate = Get-Date -UFormat "%m/%d/%Y"

# if GitRevision is $null, GIT_REVISION will be excluded from the Copy-FileWithReplacements
$GitRevision = & "git.exe" "rev-list" "--max-count=1" "HEAD"
if ($GitRevision) {
	Set-Content -Path ".revision" -Value $GitRevision
}

# [ordered] makes output easier to parse by humans
$Replacements = [ordered]@{
	# values determined from the build environment
	'VENDOR_NAME' = $Env:VENDOR_NAME;
	'PRODUCT_NAME' = $Env:PRODUCT_NAME;
	'VENDOR_DEVICE_ID' = $Env:VENDOR_DEVICE_ID;
	'VENDOR_PREFIX' = $Env:VENDOR_PREFIX;

	'MAJOR_VERSION' = $Env:MAJOR_VERSION;
	'MINOR_VERSION' = $Env:MINOR_VERSION;
	'MICRO_VERSION' = $Env:MICRO_VERSION;
	'BUILD_NUMBER' = $Env:BUILD_NUMBER;

	# generated values
	'GIT_REVISION' = $GitRevision;

	'INF_DATE' = $InfDate;
	'INF_ARCH' = $InfArch[$Env:Platform];
	'YEAR' = $TheYear;
	'MONTH' = $TheMonth;
	'DAY' = $TheDay
}

$Replacements | Out-String | Write-Host

$includepath = Resolve-Path $Env:IncludeDir
$sourcepath = Resolve-Path $Env:SourceDir

$src = Join-Path -Path $includepath -ChildPath "version.tmpl"
$dst = Join-Path -Path $includepath -ChildPath "version.h"
Copy-FileWithReplacements $src $dst -Replacements $Replacements

# Use the SourceDir environment variable to construct the source directory path
# Retrieve the full path of the first .inf file in the source directory
$infFilePath = Get-ChildItem -Path $Env:SourceDir -Filter "*.inf" | Select-Object -First 1 -ExpandProperty FullName

# Check if a .inf file was found
if ($infFilePath) {
    # Construct the destination path in the solution directory
	$destinationPath = Join-Path -Path $Env:SolutionDir -ChildPath (Split-Path $infFilePath -Leaf)
	Copy-FileWithReplacements $infFilePath $destinationPath -Replacements $Replacements
} else {
    Write-Host "No .inf file found in the directory $Env:SourceDir."
}


