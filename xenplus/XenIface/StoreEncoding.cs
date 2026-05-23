using System.Text;

namespace XenPlus.XenIface;

sealed class StoreEncoding : Encoding {
    public static StoreEncoding Instance = new();

    // Check* should already take care of preventing fallbacks, but this doesn't hurt.
    static readonly Encoding _safeASCIIEncoding = GetEncoding(
        "us-ascii",
        new EncoderExceptionFallback(),
        new DecoderExceptionFallback());

    static void CheckChars(char[] chars, int charIndex, int charCount) {
        var invalid = chars.AsSpan()[charIndex..(charIndex + charCount)].IndexOfAnyExceptInRange('\x20', '\x7f');
        if (invalid != -1) {
            throw new EncoderFallbackException("found out-of-range char");
        }
    }

    static void CheckBytes(byte[] bytes, int byteIndex, int byteCount) {
        var invalid = bytes.AsSpan()[byteIndex..(byteIndex + byteCount)].IndexOfAnyExceptInRange<byte>(0x20, 0x7f);
        if (invalid != -1) {
            throw new DecoderFallbackException("found out-of-range byte", bytes, invalid);
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
        return _safeASCIIEncoding.GetMaxByteCount(charCount);
    }

    public override int GetMaxCharCount(int byteCount) {
        return _safeASCIIEncoding.GetMaxCharCount(byteCount);
    }
}
