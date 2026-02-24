[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$Path,
    [Parameter(Mandatory)]
    [string]$ImageFilePath,
    [Parameter()]
    [string]$VolumeLabel
)

$ErrorActionPreference = "Stop"

# For unclear reasons, ComTypes.IStream works from C# but not from PowerShell
$compilerParam = [System.CodeDom.Compiler.CompilerParameters]::new()
$compilerParam.CompilerOptions = "/unsafe"
Add-Type -CompilerParameters $compilerParam -TypeDefinition @"
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace XenTools {
    public static class IsoImage {
        public static void WriteStream(object o, string filePath) {
            using (var fileStream = File.Open(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None)) {

                var stream = (IStream)o;
                try {
                    var buffer = new byte[65536];
                    uint cbRead;

                    unsafe {
                        while (true) {
                            stream.Read(buffer, buffer.Length, (IntPtr)(&cbRead));
                            if (cbRead == 0) {
                                break;
                            }
                            fileStream.Write(buffer, 0, (int)cbRead);
                        }
                    }
                }
                finally {
                    Marshal.FinalReleaseComObject(stream);
                }
            }
        }
    }
}
"@

$FsiFileSystemUDF = 4

$Path = Resolve-Path $Path
$ImageFilePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ImageFilePath)

try {
    & {
        $fsi = New-Object -ComObject IMAPI2FS.MsftFileSystemImage
        $fsi.FileSystemsToCreate = $FsiFileSystemUDF
        $fsi.FreeMediaBlocks = 0
        if ($VolumeLabel) {
            $fsi.VolumeName = $VolumeLabel
        }
        $fsi.Root.AddTree($Path, $false)

        $resultImage = $fsi.CreateResultImage()
        if ($null -eq $resultImage) {
            throw "Cannot get result image"
        }
        [XenTools.IsoImage]::WriteStream($resultImage.ImageStream, $ImageFilePath)
    }
}
finally {
    # needed to release IMAPI objects (and lingering file handles used by IMAPI)
    [System.GC]::Collect()
    [System.GC]::WaitForPendingFinalizers()
}
