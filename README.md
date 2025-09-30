# Windows PV Drivers for XCP-ng

This repo contains the Windows PV guest driver installer for XCP-ng guests.

The relevant source code may be found at these locations:

* Drivers:
    * [Bus Device Driver](https://github.com/xcp-ng/win-xenbus)
    * [Interface Driver](https://github.com/xcp-ng/win-xeniface)
    * [Network Class Driver](https://github.com/xcp-ng/win-xenvif)
    * [Network Device Driver](https://github.com/xcp-ng/win-xennet)
    * [Storage Class Driver](https://github.com/xcp-ng/win-xenvbd)
    * [Console Driver](https://github.com/xcp-ng/win-xencons)
    * [Keyboard/Mouse Driver](https://github.com/xcp-ng/win-xenvkbd)
    * [HID Minidriver](https://github.com/xcp-ng/win-xenhid)
* [Driver installation support library](XenDriverUtils/)
* [Installer package](installer/)
    * [Custom WiX actions](DriverInstallCustomAction/)
* [XenClean](XenClean/)
* [XenBootFix](XenBootFix/)
* [Xen Guest Agent](https://github.com/xcp-ng/xen-guest-agent)
    * [xenstore-win dependency](xenstore-win/)
* [Developer support scripts](scripts/)
* [Supplemental tools](extras/)
* [Source documentation](docs/)

# Usage

Driver and tool projects are included as submodules of this repository:

```
git clone --recursive https://github.com/xcp-ng/win-pv-drivers.git
```

For further information, see the [source documentation](docs/).
