using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.ActiveDirectory;

namespace XenPlus;

sealed class DsRolePrimaryDomainInfoBasicSafeHandle : SafeHandle {
    unsafe DsRolePrimaryDomainInfoBasicSafeHandle(byte* info) : base((nint)info, true) {
    }

    public static DsRolePrimaryDomainInfoBasicSafeHandle GetDsRolePrimaryDomainInfoBasic() {
        unsafe {
            byte* info = null;
            var err = PInvoke.DsRoleGetPrimaryDomainInformation(
                null,
                DSROLE_PRIMARY_DOMAIN_INFO_LEVEL.DsRolePrimaryDomainInfoBasic,
                ref info);
            if (err != (uint)WIN32_ERROR.ERROR_SUCCESS) {
                throw new Win32Exception(unchecked((int)err));
            }
            return new(info);
        }
    }

    public override bool IsInvalid => handle == nint.Zero;

    protected override bool ReleaseHandle() {
        unsafe {
            PInvoke.DsRoleFreeMemory((void*)handle);
        }
        return true;
    }

    unsafe DSROLE_PRIMARY_DOMAIN_INFO_BASIC* Info {
        get {
            ObjectDisposedException.ThrowIf(IsInvalid, this);
            return (DSROLE_PRIMARY_DOMAIN_INFO_BASIC*)handle;
        }
    }

    public DSROLE_MACHINE_ROLE MachineRole {
        get {
            unsafe {
                return Info->MachineRole;
            }
        }
    }

    public string DomainNameFlat {
        get {
            unsafe {
                return Info->DomainNameFlat.ToString();
            }
        }
    }

    public string DomainNameDns {
        get {
            unsafe {
                return Info->DomainNameDns.ToString();
            }
        }
    }

    public string DomainForestName {
        get {
            unsafe {
                return Info->DomainForestName.ToString();
            }
        }
    }

    public Guid? DomainGuid {
        get {
            unsafe {
                return ((Info->Flags & PInvoke.DSROLE_PRIMARY_DOMAIN_GUID_PRESENT) != 0) ? Info->DomainGuid : null;
            }
        }
    }
}
