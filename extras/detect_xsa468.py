#!/usr/bin/env python

from __future__ import print_function

import logging
import sys

try:
    from itertools import zip_longest
except ImportError:
    from itertools import izip_longest as zip_longest

import XenAPI

MAX_VULNERABLE = {
    "xenbus": {
        "XenServer": (9, 1, 11, 114),  # 9.1.11.115 minus 1
        "Citrix": (9, 1, 11, 114),
        "XCP_ng": (9, 0, 9048, -1),
        "Xen_Project": (9, 1, 0, 1),
    },
    "xeniface": {
        "XenServer": (9, 1, 12, 93),  # 9.1.12.94 minus 1
        "Citrix": (9, 1, 12, 93),
        "XCP_ng": (9, 0, 9048, -1),
        "Xen_Project": (9, 1, 0, 0),
    },
    "xencons": {
        "XCP_ng": (9, 0, 9048, -1),
        "Xen_Project": (9, 1, 0, 2),
    },
}


def version_lt(a, b):
    for ai, bi in zip_longest(a, b, fillvalue=0):
        if ai < bi:
            return True
        elif ai > bi:
            return False
    return False


def version_eq(a, b):
    for ai, bi in zip_longest(a, b, fillvalue=0):
        if ai != bi:
            return False
    return True


def check_vulnerable(pv_drivers_version):
    for vm_driver, vm_version_string in pv_drivers_version.items():
        driver_data = {
            "vm_driver": vm_driver,
            "verdict": False,
        }
        logging.debug(
            "Looking into driver %s, version %s", vm_driver, vm_version_string
        )
        if vm_driver in ["major", "minor", "micro", "build"]:
            continue
        if vm_driver not in MAX_VULNERABLE:
            yield driver_data
            continue
        logging.debug(
            "Found interesting driver %s version %s",
            vm_driver,
            vm_version_string,
        )
        vm_vendor, vm_vernum, vm_otherver = vm_version_string.split(" ", 3)
        driver_data["vm_vendor"] = vm_vendor
        logging.debug("%s|%s|%s", vm_vendor, vm_vernum, vm_otherver)
        try:
            vm_driverver = tuple(int(x) for x in vm_vernum.split("."))
        except:
            logging.error("Error parsing driver version %s", vm_vernum)
            yield driver_data
            continue
        driver_data["vm_driverver"] = vm_driverver
        logging.debug("Found driver version %s", vm_driverver)

        tocheck = MAX_VULNERABLE[vm_driver]
        if vm_vendor not in tocheck:
            yield driver_data
            continue
        logging.debug("Vendor %s is affected", vm_vendor)
        driver_data["max_vulnerable"] = max_vulnerable = tocheck[vm_vendor]
        logging.debug("Max vulnerable version is %s", max_vulnerable)
        if not version_lt(max_vulnerable, vm_driverver):
            driver_data["verdict"] = True
        yield driver_data


def selftest():
    def assert_ver_eq(a, b):
        assert version_eq(a, b)
        assert version_eq(b, a)

    def assert_ver_ne(a, b):
        assert not version_eq(a, b)
        assert not version_eq(b, a)

    def assert_ver_lt(a, b):
        assert version_lt(a, b)
        assert_ver_ne(a, b)
        assert not version_lt(b, a)

    def assert_vulnerable(r):
        assert any(x["verdict"] for x in check_vulnerable(r))

    def assert_not_vulnerable(r):
        assert all(not x["verdict"] for x in check_vulnerable(r))

    assert_ver_eq((9, 1), (9, 1))
    assert_ver_eq((9, 1), (9, 1, 0))
    assert_ver_eq((9, 1), (9, 1, 0, 0))

    assert_ver_ne((9, 1), (9, 2))
    assert_ver_ne((9, 1), (9, 1, 1))
    assert_ver_ne((9, 1), (9, 1, 1))

    assert_ver_lt((9, 1, 0, 0), (9, 1, 0, 1))
    assert_ver_lt((9, 1, 0), (9, 1, 0, 1))
    assert_ver_lt((9, 1, 0, -1), (9, 1))
    assert_ver_lt((9, 1, 0, -1), (9, 1, 0, 0))
    assert_ver_lt((9, 1, 0, -1), (9, 1, 0, 0, 0, 0))
    assert_ver_lt((9, 1, 0, -1), (9, 1, 0, 0, 0, -1))
    assert_ver_lt((9, 1), (9, 2, 0))
    assert_ver_lt((9, 1, 0), (9, 2))

    assert_vulnerable(
        {
            "xenbus": "XenServer 9.1.9.105 ",
        }
    )

    assert_vulnerable(
        {
            "xenbus": "XenServer 9.1.11.114 ",
        }
    )

    assert_not_vulnerable(
        {
            "xenbus": "XenServer 9.1.11.115 ",
        }
    )

    assert_not_vulnerable(
        {
            "xenbus": "XenServer 9.1.11.116 ",
        }
    )

    assert_not_vulnerable(
        {
            "major": "9",
            "minor": "4",
            "micro": "0",
            "build": "146",
        }
    )


if __name__ == "__main__":
    selftest()
    if "--debug" in sys.argv:
        logging.getLogger().setLevel(level=logging.DEBUG)
    logging.debug("Selftest OK")

    logging.warning(
        "This script does not exhaustively detect all versions of Xen PV drivers vulnerable to XSA-468."
    )

    session = XenAPI.xapi_local()
    session.xenapi.login_with_password(
        "root", "", XenAPI.API_VERSION_1_1, "detect_xsa468"
    )
    try:
        vms = session.xenapi.VM.get_all()
        for vm in vms:
            logging.debug("scanning VM %s", vm)
            try:
                vm_uuid = session.xenapi.VM.get_uuid(vm)
                vm_name_label = session.xenapi.VM.get_name_label(vm)
                if session.xenapi.VM.get_platform(vm).get("device_id") != "0002":
                    logging.debug("not Windows VM, continuing")
                    continue
                gm_ref = session.xenapi.VM.get_guest_metrics(vm)
                if not session.xenapi.VM_guest_metrics.get_PV_drivers_detected(gm_ref):
                    logging.debug("PV drivers not detected, continuing")
                    continue
                pv_drivers = session.xenapi.VM_guest_metrics.get_PV_drivers_version(
                    gm_ref
                )
            except XenAPI.Failure as ex:
                logging.debug("cannot scan %s: %s", vm, ex)
                continue
            all_data = list(check_vulnerable(pv_drivers))
            if all_data:
                for driver_data in all_data:
                    if driver_data["verdict"]:
                        logging.warning(
                            "Found vulnerable VM: {vm_uuid} ({vm_name_label}) running {vm_vendor} {vm_driver} {vm_driverver} <= {max_vulnerable}".format(
                                vm_uuid=vm_uuid,
                                vm_name_label=vm_name_label,
                                **driver_data
                            )
                        )
            else:
                logging.warning(
                    "Cannot determine if VM %s (%s) is vulnerable. Maybe you need to update XCP-ng, or the VM wasn't started recently?",
                    vm_uuid,
                    vm_name_label,
                )
    finally:
        session.xenapi.logout()
