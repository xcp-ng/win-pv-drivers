using System.ComponentModel;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Networking.ActiveDirectory;

namespace XenPlus;

sealed class DsRolePrimaryDomainInfoBasicSafeHandle : DsRoleFreeMemorySafeHandle {
    unsafe DsRolePrimaryDomainInfoBasicSafeHandle(byte* info, bool ownsHandle) : base((nint)info, ownsHandle) {
    }

    public static DsRolePrimaryDomainInfoBasicSafeHandle GetDsRolePrimaryDomainInfoBasic() {
        unsafe {
            byte* info = null;
            var err = PInvoke.DsRoleGetPrimaryDomainInformation(
                null,
                DSROLE_PRIMARY_DOMAIN_INFO_LEVEL.DsRolePrimaryDomainInfoBasic,
                ref info);
            if (err != (uint)WIN32_ERROR.ERROR_SUCCESS) {
                throw new Win32Exception(unchecked((int)err), nameof(PInvoke.DsRoleGetPrimaryDomainInformation));
            }
            return new(info, true);
        }
    }

    public DSROLE_MACHINE_ROLE MachineRole {
        get {
            using var shref = this.Borrow();
            unsafe {
                return ((DSROLE_PRIMARY_DOMAIN_INFO_BASIC*)shref.Handle)->MachineRole;
            }
        }
    }

    public string DomainNameFlat {
        get {
            using var shref = this.Borrow();
            unsafe {
                return ((DSROLE_PRIMARY_DOMAIN_INFO_BASIC*)shref.Handle)->DomainNameFlat.ToString();
            }
        }
    }

    public string DomainNameDns {
        get {
            using var shref = this.Borrow();
            unsafe {
                return ((DSROLE_PRIMARY_DOMAIN_INFO_BASIC*)shref.Handle)->DomainNameDns.ToString();
            }
        }
    }

    public string DomainForestName {
        get {
            using var shref = this.Borrow();
            unsafe {
                return ((DSROLE_PRIMARY_DOMAIN_INFO_BASIC*)shref.Handle)->DomainForestName.ToString();
            }
        }
    }

    public Guid? DomainGuid {
        get {
            using var shref = this.Borrow();
            unsafe {
                var info = (DSROLE_PRIMARY_DOMAIN_INFO_BASIC*)shref.Handle;
                return ((info->Flags & PInvoke.DSROLE_PRIMARY_DOMAIN_GUID_PRESENT) != 0) ? info->DomainGuid : null;
            }
        }
    }
}
