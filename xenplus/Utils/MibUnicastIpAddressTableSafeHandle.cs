using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;

namespace XenPlus;

sealed class MibUnicastIpAddressTableSafeHandle : SafeHandle, IReadOnlyList<MIB_UNICASTIPADDRESS_ROW> {
    unsafe MibUnicastIpAddressTableSafeHandle(MIB_UNICASTIPADDRESS_TABLE* table) : base((nint)table, true) {
    }

    public static MibUnicastIpAddressTableSafeHandle GetUnicastIpAddressTable(ADDRESS_FAMILY family) {
        unsafe {
            var err = PInvoke.GetUnicastIpAddressTable(family, out var table);
            if (err != Windows.Win32.Foundation.WIN32_ERROR.NO_ERROR) {
                throw new Win32Exception((int)err);
            }
            return new MibUnicastIpAddressTableSafeHandle(table);
        }
    }

    public override bool IsInvalid => handle == nint.Zero;

    protected override bool ReleaseHandle() {
        unsafe {
            PInvoke.FreeMibTable((void*)handle);
        }
        return true;
    }

    unsafe MIB_UNICASTIPADDRESS_TABLE* Table {
        get {
            return (MIB_UNICASTIPADDRESS_TABLE*)handle;
        }
    }

    public int Count {
        get {
            using var shref = this.Borrow();
            unsafe {
                return (int)Table->NumEntries;
            }
        }
    }

    public MIB_UNICASTIPADDRESS_ROW this[int index] {
        get {
            using var shref = this.Borrow();
            unsafe {
                if (index < 0 || index >= (int)Table->NumEntries) {
                    throw new IndexOutOfRangeException();
                }
                return Table->Table[index];
            }
        }
    }

    public IEnumerator<MIB_UNICASTIPADDRESS_ROW> GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public sealed class Enumerator(MibUnicastIpAddressTableSafeHandle _handle) : IEnumerator<MIB_UNICASTIPADDRESS_ROW> {
        int _index = -1;

        public object Current => _handle[_index];

        MIB_UNICASTIPADDRESS_ROW IEnumerator<MIB_UNICASTIPADDRESS_ROW>.Current => _handle[_index];

        public void Dispose() {
        }

        public bool MoveNext() {
            var next = _index + 1;
            if (next < _handle.Count) {
                _index = next;
                return true;
            } else {
                return false;
            }
        }

        public void Reset() {
            _index = -1;
        }
    }
}
