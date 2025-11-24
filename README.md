# XCP-ng Windows Guest Tools

This repository contains the XCP-ng Windows Guest Tools installer source code.

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
    * [xenstore-win dependency](https://github.com/xcp-ng/xenstore-win)
* [Xen Time Provider](https://github.com/xcp-ng/win-xentimeprovider)
* [Developer support scripts](scripts/)
* [Supplemental tools](extras/)
* [Source documentation](docs/)

# Usage

Driver and tool projects are included as submodules of this repository:

```
git clone --recursive https://github.com/xcp-ng/win-pv-drivers.git
```

For further information, see the [source documentation](docs/).
