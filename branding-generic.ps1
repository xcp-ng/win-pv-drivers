if ([string]::IsNullOrEmpty($Env:VENDOR_NAME)) {
    Set-Item -Path Env:VENDOR_NAME -Value 'Xen Project'
}

if ([string]::IsNullOrEmpty($Env:VENDOR_PREFIX)) {
    Set-Item -Path Env:VENDOR_PREFIX -Value 'XP'
}

if ([string]::IsNullOrEmpty($Env:PRODUCT_NAME)) {
    Set-Item -Path Env:PRODUCT_NAME -Value 'Xen'
}

if ([string]::IsNullOrEmpty($Env:COPYRIGHT)) {
    Set-Item -Path Env:COPYRIGHT -Value 'Copyright (c) Xen Project.'
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

if ([string]::IsNullOrEmpty($Env:MAJOR_VERSION)) {
    Set-Item -Path Env:MAJOR_VERSION -Value '9'
}

if ([string]::IsNullOrEmpty($Env:MINOR_VERSION)) {
    Set-Item -Path Env:MINOR_VERSION -Value '1'
}

if ([string]::IsNullOrEmpty($Env:MICRO_VERSION)) {
    Set-Item -Path Env:MICRO_VERSION -Value '0'
}

if ([string]::IsNullOrEmpty($Env:MSI_UPGRADE_CODE_X86)) {
    Set-Item -Path Env:MSI_UPGRADE_CODE_X86 -Value '{10828840-D8A9-4953-B44A-1F1D3CD7ECB0}'
}

if ([string]::IsNullOrEmpty($Env:MSI_UPGRADE_CODE_X64)) {
    Set-Item -Path Env:MSI_UPGRADE_CODE_X64 -Value '{D60FED1E-316C-41B0-B7A5-E44951A82618}'
}
