# Windows PV Drivers Installation Guide for XCP-ng

This document describes the steps to install Windows PV drivers generated for the XCP-ng project on a Windows guest system and troubleshoot setup issues

## Contents of the Output Directory

After a successful driver build, the output directory will contain the following files:

- managementagentx64.msi - Installer for 64-bit systems.
- managementagentx86.msi - Installer for 32-bit systems.
- Setup.exe - Installation program.
- win-pv-drivers.zip - Archive containing all the drivers.
- XCP-ng-1704878479922986200.cer - Certificate for driver signing.

## Copy build output to the target Testing System

To facilitate testing Windows PV drivers on a Windows guest system, it is recommended to use an efficient synchronization method between the development machine and the testing machine. One of the suggested methods is to use a cloud client, such as NextCloud, installed on both machines to automatically synchronize the build's output directory with the testing Windows guest system. You may install a Cloud Client (e.g., NextCloud) on both machines to automatically sync the build's output directory with the testing Windows guest system.

## Setup the Windows machine for test signed drivers

    ### Enable Test Mode (for accepting self-signed drivers):
        - Disable Driver Signature Enforcement:
            Command: bcdedit /set nointegritychecks on
            This command disables driver signature enforcement, allowing the installation of unsigned drivers.
        - Enable Test Mode:
            Command: bcdedit /set testsigning on
            Enabling Test Mode allows you to install unsigned drivers for testing purposes.

    ### Create a Clean Snapshot of the Testing System*
		Prepare a clean state of the Windows guest system with necessary updates, the cloud client installed, and without any pre-existing Xen drivers. Create a snapshot of this state for easy restoration after each round of testing. This ensures a consistent testing environment for each build.

## Install the Drivers
Once the build output is copied or synchronized on the target guest, proceed with installation, which is a straightforward process.

## Analyzing Driver Installation Errors

With the current build, there are some issues to address

### Using the setupapi.dev.log File

https://learn.microsoft.com/en-us/windows-hardware/drivers/install/setupapi-text-logs
The setupapi.dev.log file is a crucial log file in Windows that traces operations related to driver installation. This file records detailed information about driver addition, removal, and update processes, including:

    The commands executed during driver installation or update.
    The paths of .inf files and other files associated with the driver.
    The results of driver signature checks.
    Errors encountered during various installation steps.

By analyzing this file, you can diagnose issues encountered during driver installation, such as signature failures, driver conflicts, and other compatibility or configuration problems.

Example:

After installing existing drivers (tested, official build), you will obtain traces like the following:

>>>  [SetupCopyOEMInf - C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.inf]
>>>  Section start 2023/11/09 07:23:02.062
      cmd: "C:\Program Files\XCP-ng\XenTools\InstallAgent.exe"
     inf: Copy style: 0x00000008
     sto: {Setup Import Driver Package: C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.inf} 07:23:02.125
     inf:      Provider: XCP-ng
     inf:      Class GUID: {4d36e972-e325-11ce-bfc1-08002be10318}
     inf:      Driver Version: 06/26/2019,8.2.2.0
     inf:      Catalog File: xennet.cat
     ump:      Import flags: 0x00000009
     pol:      {Driver package policy check} 07:23:02.297
     pol:      {Driver package policy check - exit(0x00000000)} 07:23:02.297
     sto:      {Stage Driver Package: C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.inf} 07:23:02.297
     inf:           {Query Configurability: C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.inf} 07:23:02.297
!    inf:                Found legacy AddReg operation defining co-installers (CoInstallers32). Code = 1303
!    inf:                Driver package 'xennet.inf' is NOT configurable.
     inf:           {Query Configurability: exit(0x00000000)} 07:23:02.312
     flq:           {FILE_QUEUE_COMMIT} 07:23:02.312
     flq:                Copying 'C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.inf' to 'C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet.inf'.
     flq:                Copying 'C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.sys' to 'C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet.sys'.
     flq:                Copying 'C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet_coinst.dll' to 'C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet_coinst.dll'.
     flq:                Copying 'C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.cat' to 'C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet.cat'.
     flq:           {FILE_QUEUE_COMMIT - exit(0x00000000)} 07:23:02.453
     sto:           {DRIVERSTORE IMPORT VALIDATE} 07:23:02.453
     sig:                Driver package catalog is valid.
     sig:                {_VERIFY_FILE_SIGNATURE} 07:23:02.500
     sig:                     Key      = xennet.inf
     sig:                     FilePath = C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet.inf
     sig:                     Catalog  = C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet.cat
!    sig:                     Verifying file against specific (valid) catalog failed.
!    sig:                     Error 0x800b0109: A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.
     sig:                {_VERIFY_FILE_SIGNATURE exit(0x800b0109)} 07:23:02.547
     sig:                {_VERIFY_FILE_SIGNATURE} 07:23:02.547
     sig:                     Key      = xennet.inf
     sig:                     FilePath = C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet.inf
     sig:                     Catalog  = C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet.cat
     sig:                     Success: File is signed in Authenticode(tm) catalog.
     sig:                     Error 0xe0000241: The INF was signed with an Authenticode(tm) catalog from a trusted publisher.
     sig:                {_VERIFY_FILE_SIGNATURE exit(0xe0000241)} 07:23:02.609
     sto:           {DRIVERSTORE IMPORT VALIDATE: exit(0x00000000)} 07:23:02.625
     sig:           Signer Score  = 0x0F000000 (Authenticode)
     sig:           Signer Name   = Vates
     sto:           {Core Driver Package Import: xennet.inf_amd64_c24936213eb06e51} 07:23:02.625
     sto:                {DRIVERSTORE IMPORT BEGIN} 07:23:02.640
     sto:                {DRIVERSTORE IMPORT BEGIN: exit(0x00000000)} 07:23:02.640
     cpy:                {Copy Directory: C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}} 07:23:02.640
     cpy:                     Target Path = C:\WINDOWS\System32\DriverStore\FileRepository\xennet.inf_amd64_c24936213eb06e51
     cpy:                {Copy Directory: exit(0x00000000)} 07:23:02.640
     idb:                {Register Driver Package: C:\WINDOWS\System32\DriverStore\FileRepository\xennet.inf_amd64_c24936213eb06e51\xennet.inf} 07:23:02.640
     idb:                     Created driver package object 'xennet.inf_amd64_c24936213eb06e51' in DRIVERS database node.
     idb:                     Created driver INF file object 'oem2.inf' in DRIVERS database node.
     idb:                     Registered driver package 'xennet.inf_amd64_c24936213eb06e51' with 'oem2.inf'.
     idb:                {Register Driver Package: exit(0x00000000)} 07:23:02.656
     idb:                {Publish Driver Package: C:\WINDOWS\System32\DriverStore\FileRepository\xennet.inf_amd64_c24936213eb06e51\xennet.inf} 07:23:02.656
     idb:                     Activating driver package 'xennet.inf_amd64_c24936213eb06e51'.
     cpy:                     Published 'xennet.inf_amd64_c24936213eb06e51\xennet.inf' to 'oem2.inf'.
     idb:                     Indexed 4 device IDs for 'xennet.inf_amd64_c24936213eb06e51'.
     sto:                     Flushed driver database node 'DRIVERS'. Time = 0 ms
     sto:                     Flushed driver database node 'SYSTEM'. Time = 16 ms
     idb:                {Publish Driver Package: exit(0x00000000)} 07:23:02.687
     sto:                {DRIVERSTORE IMPORT END} 07:23:02.687
     dvi:                     Flushed all driver package files to disk. Time = 15 ms
     sig:                     Installed catalog 'xennet.cat' as 'oem2.cat'.
     sto:                {DRIVERSTORE IMPORT END: exit(0x00000000)} 07:23:02.906
     sto:           {Core Driver Package Import: exit(0x00000000)} 07:23:02.906
     sto:      {Stage Driver Package: exit(0x00000000)} 07:23:02.922
     sto: {Setup Import Driver Package - exit (0x00000000)} 07:23:02.984
     inf: Driver Store Path: C:\WINDOWS\System32\DriverStore\FileRepository\xennet.inf_amd64_c24936213eb06e51\xennet.inf
     inf: Published Inf Path: C:\WINDOWS\INF\oem2.inf
<<<  Section end 2023/11/09 07:23:03.031
<<<  [Exit status: SUCCESS]


After installing generated drivers, you will obtain traces like the following (example for the xennet driver):

>>>  [SetupCopyOEMInf - C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.inf]
>>>  Section start 2023/12/15 13:50:24.770
      cmd: "C:\Program Files\XCP-ng\XenTools\InstallAgent.exe"
     inf: Copy style: 0x00000008
     sto: {Setup Import Driver Package: C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.inf} 13:50:24.848
     inf:      Provider: XCP new generation
     inf:      Class GUID: {4d36e972-e325-11ce-bfc1-08002be10318}
     inf:      Driver Version: 12/15/2023,9.1.0.0
     inf:      Catalog File: xennet.cat
     ump:      Import flags: 0x00000009
     pol:      {Driver package policy check} 13:50:25.005
     pol:      {Driver package policy check - exit(0x00000000)} 13:50:25.005
     sto:      {Stage Driver Package: C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.inf} 13:50:25.005
     inf:           {Query Configurability: C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.inf} 13:50:25.020
     inf:                Driver package 'xennet.inf' is configurable.
     inf:           {Query Configurability: exit(0x00000000)} 13:50:25.036
     flq:           {FILE_QUEUE_COMMIT} 13:50:25.036
     flq:                Copying 'C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.inf' to 'C:\WINDOWS\System32\DriverStore\Temp\{b65166fc-60c4-3b46-afea-7b9e26857475}\xennet.inf'.
     flq:                Copying 'C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.sys' to 'C:\WINDOWS\System32\DriverStore\Temp\{b65166fc-60c4-3b46-afea-7b9e26857475}\xennet.sys'.
     flq:                Copying 'C:\Program Files\XCP-ng\XenTools\Drivers\xennet\x64\xennet.cat' to 'C:\WINDOWS\System32\DriverStore\Temp\{b65166fc-60c4-3b46-afea-7b9e26857475}\xennet.cat'.
     flq:           {FILE_QUEUE_COMMIT - exit(0x00000000)} 13:50:25.098
     sto:           {DRIVERSTORE IMPORT VALIDATE} 13:50:25.098
     sig:                Driver package catalog is valid.
     sig:                {_VERIFY_FILE_SIGNATURE} 13:50:25.145
     sig:                     Key      = xennet.inf
     sig:                     FilePath = C:\WINDOWS\System32\DriverStore\Temp\{b65166fc-60c4-3b46-afea-7b9e26857475}\xennet.inf
     sig:                     Catalog  = C:\WINDOWS\System32\DriverStore\Temp\{b65166fc-60c4-3b46-afea-7b9e26857475}\xennet.cat
!    sig:                     Verifying file against specific (valid) catalog failed.
!    sig:                     Error 0x800b0109: A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.
     sig:                {_VERIFY_FILE_SIGNATURE exit(0x800b0109)} 13:50:25.192
     sig:                {_VERIFY_FILE_SIGNATURE} 13:50:25.192
     sig:                     Key      = xennet.inf
     sig:                     FilePath = C:\WINDOWS\System32\DriverStore\Temp\{b65166fc-60c4-3b46-afea-7b9e26857475}\xennet.inf
     sig:                     Catalog  = C:\WINDOWS\System32\DriverStore\Temp\{b65166fc-60c4-3b46-afea-7b9e26857475}\xennet.cat
!    sig:                     Verifying file against specific Authenticode(tm) catalog failed.
!    sig:                     Error 0x800b0109: A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.
     sig:                {_VERIFY_FILE_SIGNATURE exit(0x800b0109)} 13:50:25.208
!!!  sig:                Catalog signer is untrusted. No error message will be displayed as client is running in non-interactive mode.
!!!  sig:                Driver package failed signature validation. Error = 0xE0000247
     sto:           {DRIVERSTORE IMPORT VALIDATE: exit(0xe0000247)} 13:50:25.223
!!!  sig:           Driver package failed signature verification. Error = 0xE0000247
!!!  sto:           Failed to import driver package into Driver Store. Error = 0xE0000247
     sto:      {Stage Driver Package: exit(0xe0000247)} 13:50:25.223
     sto: {Setup Import Driver Package - exit (0xe0000247)} 13:50:25.255
!!!  inf: Failed to import driver package into driver store
!!!  inf: Error 0xe0000247: A problem was encountered while attempting to add the driver to the store.
<<<  Section end 2023/12/15 13:50:25.364
<<<  [Exit status: FAILURE(0xe0000247)]

These errors should be analyzed in light of the expected traces.

As we can obeserve, the 2 log traces diverge at some point after the {_VERIFY_FILE_SIGNATURE} item in {DRIVERSTORE IMPORT VALIDATE}

     flq:           {FILE_QUEUE_COMMIT - exit(0x00000000)} 07:23:02.453
     sto:           {DRIVERSTORE IMPORT VALIDATE} 07:23:02.453
     sig:                Driver package catalog is valid.
     sig:                {_VERIFY_FILE_SIGNATURE} 07:23:02.500
     sig:                     Key      = xennet.inf
     sig:                     FilePath = C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet.inf
     sig:                     Catalog  = C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet.cat
!    sig:                     Verifying file against specific (valid) catalog failed.
!    sig:                     Error 0x800b0109: A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.

In both cases, the first attempt to {_VERIFY_FILE_SIGNATURE} fail, but in the case of the working build, there is a sort of failover on a second try which ends up with status "0xe0000241: The INF was signed with an Authenticode(tm) catalog from a trusted publisher."

     sig:                {_VERIFY_FILE_SIGNATURE} 07:23:02.547
     sig:                     Key      = xennet.inf
     sig:                     FilePath = C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet.inf
     sig:                     Catalog  = C:\WINDOWS\System32\DriverStore\Temp\{16336670-c71d-ad40-80a6-846e33c5a3be}\xennet.cat
     sig:                     Success: File is signed in Authenticode(tm) catalog.
     sig:                     Error 0xe0000241: The INF was signed with an Authenticode(tm) catalog from a trusted publisher.
     sig:                {_VERIFY_FILE_SIGNATURE exit(0xe0000241)} 07:23:02.609
	 
In the case of the current build, the failover ends up again with same status:

     sig:                {_VERIFY_FILE_SIGNATURE} 13:50:25.192
     sig:                     Key      = xennet.inf
     sig:                     FilePath = C:\WINDOWS\System32\DriverStore\Temp\{b65166fc-60c4-3b46-afea-7b9e26857475}\xennet.inf
     sig:                     Catalog  = C:\WINDOWS\System32\DriverStore\Temp\{b65166fc-60c4-3b46-afea-7b9e26857475}\xennet.cat
!    sig:                     Verifying file against specific Authenticode(tm) catalog failed.
!    sig:                     Error 0x800b0109: A certificate chain processed, but terminated in a root certificate which is not trusted by the trust provider.
     sig:                {_VERIFY_FILE_SIGNATURE exit(0x800b0109)} 13:50:25.208
!!!  sig:                Catalog signer is untrusted. No error message will be displayed as client is running in non-interactive mode.
!!!  sig:                Driver package failed signature validation. Error = 0xE0000247

