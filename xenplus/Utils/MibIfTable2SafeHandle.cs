using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.NetworkManagement.IpHelper;

sealed class MibIfTable2SafeHandle : SafeHandle {

    unsafe MibIfTable2SafeHandle(MIB_IF_TABLE2* table) : base((nint)table, true) {
    }

    public static MibIfTable2SafeHandle GetIfTable2() {
        unsafe {
            var err = PInvoke.GetIfTable2(out var table);
            if (err != Windows.Win32.Foundation.WIN32_ERROR.NO_ERROR) {
                throw new Win32Exception((int)err);
            }
            return new MibIfTable2SafeHandle(table);
        }
    }

    public override bool IsInvalid => DangerousGetHandle() == nint.Zero;

    protected override bool ReleaseHandle() {
        unsafe {
            PInvoke.FreeMibTable((void*)DangerousGetHandle());
        }
        return true;
    }

    public List<MIB_IF_ROW2> GetRows() {
        List<MIB_IF_ROW2> result = [];

        if (IsInvalid) {
            return result;
        }

        unsafe {
            var table = (MIB_IF_TABLE2*)DangerousGetHandle();
            foreach (var row in table->Table.AsSpan((int)table->NumEntries)) {
                result.Add(row);
            }
        }
        return result;
    }
}
