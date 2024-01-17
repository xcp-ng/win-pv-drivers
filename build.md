
# Building Windows PV Drivers for XCP-ng

This document outlines the steps to build Windows PV drivers for the XCP-ng project. The process includes cloning the driver repository, setting up the build environment using the Enterprise Windows Driver Kit (EWDK), and executing the build scripts with improved command handling.

## Prerequisites

- **Git**: For cloning repositories.
- **Python**: For running build scripts.
- **.Net SDK**: .Net SDK environnement It's crucial to install the .Net SDK rather than just the runtime, as the SDK contains essential tools and libraries for building the drivers that are not available in the runtime version. Ensure you have the correct version of the SDK installed, as specified in the project requirements.
- **wix toolset v4**: wix v4, a .Net based setup tool


## Configuring PowerShell Execution Policy

Before running the build script, it's necessary to ensure that your system's PowerShell execution policy allows the script to run. By default, PowerShell restricts script execution for security reasons. To change this, you need to modify the execution policy.

### Modifying the Execution Policy

1. **Open PowerShell as an Administrator**: Right-click on the PowerShell icon and select "Run as administrator".

2. **Check Current Execution Policy**: Run the following command to view the current execution policy setting:
   ```powershell
   Get-ExecutionPolicy
   ```

3. **Set Execution Policy to Unrestricted**: To allow the build script to run, change the execution policy to Unrestricted with this command:
   ```powershell
   Set-ExecutionPolicy Unrestricted -Scope CurrentUser
   ```
   This command sets the policy for the current user only and does not require administrative rights. If you need to set this for all users, remove `-Scope CurrentUser`, but be aware that this requires administrative rights and affects all users on the system.

4. **Confirm the Change**: When prompted, confirm the change by typing `Y` and pressing Enter.

5. **Verify the Change**: Run `Get-ExecutionPolicy` again to ensure the policy has been updated.

### Caution

- Setting the execution policy to Unrestricted allows all PowerShell scripts to run, which could pose security risks. It's recommended to revert to a more restrictive policy after running the build script. You can do this by executing `Set-ExecutionPolicy Restricted -Scope CurrentUser`.
- Always ensure that you trust the scripts you are executing on your system.

## Install .Net SDK and wixtoolset
.Net:
Recommanded version to be installed : v7
https://dotnet.microsoft.com/en-us/download/dotnet/7.0
Make sure you download and install SDK, not the runtime.
- For example : https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-7.0.405-windows-x64-installer
Follow instructions provided here if needed:
https://thistechbyte.com/a-step-by-step-guide-to-installing-net-dotnet-on-your-system/?swcfpc=1
wixtoolset:
Once .Net SDK env is installed, proceed with wixtoolset installation:
dotnet tool install --global wix
You can also follow detailed instructions here:
https://thistechbyte.com/how-to-install-wix-toolset-4/

## Cloning the Win-PV-Drivers Repository

1. Clone the `win-pv-drivers` repository from GitHub:

   ```bash
   git clone https://github.com/xcp-ng/win-pv-drivers
   ```

2. Checkout the working branch `jbe-rebase-win-pv-drivers`:

   ```bash
   cd win-pv-drivers
   git checkout jbe-rebase-win-pv-drivers
   ```

   The checkout process uses the branch prefix from the `win-pv-drivers` repository to fetch drivers with matching prefixed branches if they exist. If not, it falls back to the master branch. This approach is a temporary workaround and will be removed once the PRs are approved and merged into the master branch or a new repository.

## Setting Up the Build Environment

### Download the Enterprise Windows Driver Kit (EWDK)

The EWDK is a standalone, self-contained command-line environment for building drivers. It includes Visual Studio Build Tools, Windows SDK, and Windows Driver Kit (WDK).

- For Windows 10 up to version 1903: [Windows 10 version 1903 EWDK](https://learn.microsoft.com/en-us/windows-hardware/drivers/download-the-wdk#download-icon-enterprise-wdk-ewdk).
- For Windows 10 versions above 1903, Windows 11, Windows 2012 and up: [Windows 11 version 22H2 EWDK](https://learn.microsoft.com/en-us/windows-hardware/drivers/download-the-wdk#download-icon-enterprise-wdk-ewdk).

### Configuring the EWDK

1. Mount the downloaded EWDK ISO to an available drive letter, e.g., `E:`.
   You can either use the Windows UI to mount the image or use the following PowerShell command:

   ```powershell
   # Replace "chemin\vers\EWDK-image.iso" with the full path to your EWDK image.
   $imagePath = "chemin\vers\EWDK-image.iso"

   # Mount the ISO image
   Mount-DiskImage -ImagePath $imagePath
   ```
   
2. Launch the EWDK build environment:

   ```powershell
   # Get the drive letter where the ISO is mounted
   $driveLetter = (Get-DiskImage -ImagePath $imagePath | Get-Volume).DriveLetter

   # Launch the EWDK build environment
   & "$driveLetter:\LaunchBuildEnv.cmd"
	```

## The Build Process

### Preparing the Build

1. In the build shell, navigate to the `win-pv-drivers` directory:

   ```bash
   cd path\to\repositories\win-pv-drivers
   ```

2. Fetch the driver repositories:

   ```python
   python .\build.py fetch
   ```

   **Note**: The `fetch()` function in `build.py` temporarily checks out branches matching the current `win-pv-drivers` branch prefix. This will be revised once all code is merged into the master branch.

### Executing the Build

Run the build command:

```python
python .\build.py build
```

This command uses the improved command handling mechanism in `command_utils.py`, which simplifies the build process by automatically managing the build environment and dependencies.

## Output

Upon completion, the installer, agent, and driver binaries are located in the `[win-pv-drivers]\output` directory. This streamlined process ensures an efficient and error-free build, reflecting the latest enhancements in command management and environment setup.
