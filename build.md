
# Building Windows PV Drivers for XCP-ng

This document outlines the steps to build Windows PV drivers for the XCP-ng project. The process includes cloning the driver repository, setting up the build environment using the Enterprise Windows Driver Kit (EWDK), and executing the build scripts with improved command handling.

## Prerequisites

- **Git**: For cloning repositories.
- **Python**: For running build scripts.
- **.Net**: .Net environnement
- **wix toolset v4**: wix v4, a .Net based setup tool

## Install .Net and wixtoolset

.Net:
Recommanded version to be installed : v7
https://dotnet.microsoft.com/en-us/download/dotnet/7.0

Follow instructions provided here if needed:
https://thistechbyte.com/a-step-by-step-guide-to-installing-net-dotnet-on-your-system/?swcfpc=1

wixtoolset:
Once .Net env is installed, proceed with wixtoolset installation:
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
2. Launch the EWDK build environment:

   ```bash
   E:\LaunchBuildEnv.cmd
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
