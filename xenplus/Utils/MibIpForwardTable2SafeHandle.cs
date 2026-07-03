using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Networking.WinSock;
using Windows.Win32.NetworkManagement.IpHelper;

namespace XenPlus;

sealed class MibIpForwardTable2SafeHandle : SafeHandle, IReadOnlyList<MIB_IPFORWARD_ROW2> {
    unsafe MibIpForwardTable2SafeHandle(MIB_IPFORWARD_TABLE2* table) : base((nint)table, true) {
    }

    public static MibIpForwardTable2SafeHandle GetIpForwardTable2(ADDRESS_FAMILY family) {
        unsafe {
            var err = PInvoke.GetIpForwardTable2(family, out var table);
            if (err != Windows.Win32.Foundation.WIN32_ERROR.NO_ERROR) {
                throw new Win32Exception((int)err);
            }
            return new MibIpForwardTable2SafeHandle(table);
        }
    }

    public override bool IsInvalid => handle == nint.Zero;

    protected override bool ReleaseHandle() {
        unsafe {
            PInvoke.FreeMibTable((void*)handle);
        }
        return true;
    }

    unsafe MIB_IPFORWARD_TABLE2* Table {
        get {
            return (MIB_IPFORWARD_TABLE2*)handle;
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

    public MIB_IPFORWARD_ROW2 this[int index] {
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

    public IEnumerator<MIB_IPFORWARD_ROW2> GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public sealed class Enumerator(MibIpForwardTable2SafeHandle _handle) : IEnumerator<MIB_IPFORWARD_ROW2> {
        int _index = -1;

        public object Current => _handle[_index];

        MIB_IPFORWARD_ROW2 IEnumerator<MIB_IPFORWARD_ROW2>.Current => _handle[_index];

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
