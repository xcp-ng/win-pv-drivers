# Script to compare driver include/ and src/common/ directories against xenbus

param (
    [Parameter()]
    [string[]]$Drivers = @("xencons", "xenhid", "xeniface", "xennet", "xenvbd", "xenvif", "xenvkbd"),
    [Parameter()]
    $ReferenceDriver = "xenbus",
    [Parameter()]
    $SubDirs = @("include", "src/common"),
    [Parameter()]
    [switch]$ShowDiff
)

foreach ($driver in $Drivers) {
    $hasDifference = $false
    Write-Host "--- Comparing $driver against $ReferenceDriver ---"

    foreach ($subDir in $SubDirs) {
        $referencePath = Join-Path -Path $ReferenceDriver -ChildPath $subDir
        $targetPath = Join-Path -Path $driver -ChildPath $subDir

        if (-not (Test-Path -Path $referencePath -PathType Container)) { continue }
        if (-not (Test-Path -Path $targetPath -PathType Container)) { continue }

        $referenceFiles = Get-ChildItem -Path $referencePath -File
        if ($null -eq $referenceFiles) {
            continue
        }

        foreach ($refFile in $referenceFiles) {
            $targetFile = Join-Path -Path $targetPath -ChildPath $refFile.Name

            if (Test-Path -Path $targetFile -PathType Leaf) {
                $diffOutput = git diff --no-index --quiet --exit-code $refFile.FullName $targetFile 2>&1
                if ($LASTEXITCODE -ne 0) {
                    $hasDifference = $true
                    Write-Host "[$driver] MISMATCH in '$($refFile.FullName)' vs '$targetFile'"

                    if ($ShowDiff) {
                        $diffOutput = git diff --no-index $refFile.FullName $targetFile 2>&1
                        # Print the diff output
                        Write-Host ($diffOutput | Out-String)
                    }
                }
            }
        }
    }

    if (-not $hasDifference) {
        Write-Host "[$driver] OK: No content differences found in common files."
    }
}
