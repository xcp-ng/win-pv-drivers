[CmdletBinding()]
param (
    [Parameter()]
    [string[]]$Drivers = @("xenbus", "xencons", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd"),
    [Parameter()]
    [string]$Target = "Build",
    [Parameter(Mandatory)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration,
    [Parameter(Mandatory)]
    [ValidateSet("x86", "x64")]
    [string]$Platform,
    [Parameter()]
    [ValidateSet("vs2019", "vs2022")]
    [string]$SolutionDir = "vs2022",
    [Parameter()]
    [ValidateSet("Windows 10")]
    [string]$ConfigurationBase = "Windows 10",
    [Parameter()]
    [switch]$Kasan,
    [Parameter()]
    [switch]$Sign,
    [Parameter()]
    [switch]$CodeQL,
    [Parameter()]
    [string]$CodeQLPath = "codeql",
    [Parameter()]
    [string]$CodeQLSuite = "microsoft/windows-drivers@1.10.0:windows-driver-suites/recommended.qls",
    [Parameter()]
    [string]$CodeQLDatabaseDir = "$PSScriptRoot\codeql-db"
)

. $PSScriptRoot\branding.ps1
. $PSScriptRoot\scripts\branding-generic.ps1
. $PSScriptRoot\scripts\sign.ps1

$ErrorActionPreference = "Stop"

# Drivers are ordered by build date first so the Hmm gives you a more granular revision number (down to the minute).
$DriverTime = (Get-Date -Format Hmm)

$OutputPath = "$PSScriptRoot\driver-bins"
Remove-Item -Path $OutputPath -Force -Recurse -ErrorAction SilentlyContinue

foreach ($repo in $Drivers) {
    Push-Location $PSScriptRoot\$repo
    try {
        git clean -fXd

        $DriverConfigShort = "$ConfigurationBase$Configuration".Replace(" ", "")
        $DriverOutput = "$OutputPath\$Platform\$Configuration\$repo"

        if ($CodeQL) {
            $Database = "$CodeQLDatabaseDir\$repo"

            $BuildCommand = "powershell.exe -File ..\build-driver.ps1 ` 
                -Driver $repo `
                -Target $Target `
                -Configuration $Configuration `
                -Platform $Platform `
                -SolutionDir $SolutionDir `
                -BuildNumber $DriverTime"

            if ($Kasan) {
                $BuildCommand += " -Kasan"
            }

            & $CodeQLPath database create $Database `
                --language=cpp `
                --source-root=. `
                --overwrite `
                "--command=$BuildCommand"
            if ($LASTEXITCODE -ne 0) {
                throw "CodeQL database create failed with error $LASTEXITCODE"
            }
        }
        else {
            & $PSScriptRoot\build-driver.ps1 `
                -Driver $repo `
                -Target $Target `
                -Configuration $Configuration `
                -Platform $Platform `
                -SolutionDir $SolutionDir `
                -ConfigurationBase $ConfigurationBase `
                -BuildNumber $DriverTime `
                -Kasan:$Kasan
        }

        New-Item -ItemType Directory -Path $DriverOutput -Force
        Copy-Item -Path .\$SolutionDir\$DriverConfigShort\$Platform\package\* `
            -Destination $DriverOutput\ -Force -Recurse

        if ($CodeQL) {
            & $CodeQLPath database analyze $Database $CodeQLSuite `
                --format=sarifv2.1.0 `
                "--output=$DriverOutput\$repo.sarif"
            if ($LASTEXITCODE -ne 0) {
                throw "CodeQL database analyze failed with error $LASTEXITCODE"
            }
        }

        if ($Sign) {
            Set-SignerFileSignature $DriverOutput\*.sys, $DriverOutput\*.dll, `
                $DriverOutput\*.exe, $DriverOutput\*.cat
        }
    }
    finally {
        Pop-Location
    }
}
