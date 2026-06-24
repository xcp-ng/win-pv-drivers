using System.Text;

namespace XenPlus.XenIface;

sealed class StrictStoreEncoding : Encoding {
    public static StrictStoreEncoding Instance = new();

    // Check* should already take care of preventing fallbacks, but this doesn't hurt.
    static readonly Encoding _safeASCIIEncoding = GetEncoding(
        "us-ascii",
        new EncoderExceptionFallback(),
        new DecoderExceptionFallback());

    static void CheckChars(char[] chars, int index, int count) {
        ArgumentNullException.ThrowIfNull(chars);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, chars.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index + count, chars.Length);
        foreach (var c in chars.AsSpan()[index..(index + count)]) {
            if (!((c >= '\x20' && c <= '\x7f') || c == '\n')) {
                throw new EncoderFallbackException("found out-of-range char");
            }
        }
    }

    static void CheckBytes(byte[] bytes, int index, int count) {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, bytes.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index + count, bytes.Length);
        for (var i = index; i < index + count; i++) {
            byte b = bytes[i];
            if (!((b >= 0x20 && b <= 0x7f) || b == 10)) {
                throw new DecoderFallbackException("found out-of-range byte", bytes, i);
            }
        }
    }

    public override int GetByteCount(char[] chars, int index, int count) {
        CheckChars(chars, index, count);
        return _safeASCIIEncoding.GetByteCount(chars, index, count);
    }

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
        CheckChars(chars, charIndex, charCount);
        return _safeASCIIEncoding.GetBytes(chars, charIndex, charCount, bytes, byteIndex);
    }

    public override int GetCharCount(byte[] bytes, int index, int count) {
        CheckBytes(bytes, index, count);
        return _safeASCIIEncoding.GetCharCount(bytes, index, count);
    }

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
        CheckBytes(bytes, byteIndex, byteCount);
        return _safeASCIIEncoding.GetChars(bytes, byteIndex, byteCount, chars, charIndex);
    }

    public override int GetMaxByteCount(int charCount) {
        ArgumentOutOfRangeException.ThrowIfNegative(charCount);
        return _safeASCIIEncoding.GetMaxByteCount(charCount);
    }

    public override int GetMaxCharCount(int byteCount) {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
        return _safeASCIIEncoding.GetMaxCharCount(byteCount);
    }
}
