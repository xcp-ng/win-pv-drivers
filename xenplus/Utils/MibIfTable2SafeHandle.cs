using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.IpHelper;

namespace XenPlus;

sealed class MibIfTable2SafeHandle : SafeHandle, IReadOnlyList<MIB_IF_ROW2> {
    unsafe MibIfTable2SafeHandle(MIB_IF_TABLE2* table) : base((nint)table, true) {
    }

    public static MibIfTable2SafeHandle GetIfTable2() {
        unsafe {
            var err = PInvoke.GetIfTable2(out var table);
            if (err != WIN32_ERROR.NO_ERROR) {
                throw new Win32Exception(unchecked((int)err), nameof(PInvoke.GetIfTable2));
            }
            return new MibIfTable2SafeHandle(table);
        }
    }

    public override bool IsInvalid => handle == nint.Zero;

    protected override bool ReleaseHandle() {
        unsafe {
            PInvoke.FreeMibTable((void*)handle);
        }
        return true;
    }

    unsafe MIB_IF_TABLE2* Table {
        get {
            return (MIB_IF_TABLE2*)handle;
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

    public MIB_IF_ROW2 this[int index] {
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

    public IEnumerator<MIB_IF_ROW2> GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public sealed class Enumerator(MibIfTable2SafeHandle _handle) : IEnumerator<MIB_IF_ROW2> {
        int _index = -1;

        public object Current => _handle[_index];

        MIB_IF_ROW2 IEnumerator<MIB_IF_ROW2>.Current => _handle[_index];

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
