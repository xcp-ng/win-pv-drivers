using System.Text;

namespace XenPlus.XenIface;

sealed class StoreEncoding : Encoding {
    public static StoreEncoding Instance = new();

    public override int GetByteCount(char[] chars, int index, int count) {
        ArgumentNullException.ThrowIfNull(chars);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, chars.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index + count, chars.Length);
        return count;
    }

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
        GetByteCount(chars, charIndex, charCount);
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(byteIndex, bytes.Length);
        for (int i = 0; i < charCount; i++) {
            char c = chars[charIndex + i];
            if ((c >= '\x20' && c <= '\x7f') || c == '\r' || c == '\n') {
                bytes[byteIndex + i] = (byte)c;
            } else {
                bytes[byteIndex + i] = (byte)'?';
            }
        }
        return charCount;
    }

    public override int GetCharCount(byte[] bytes, int index, int count) {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index, bytes.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(index + count, bytes.Length);
        return count;
    }

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
        GetCharCount(bytes, byteIndex, byteCount);
        ArgumentNullException.ThrowIfNull(chars);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(charIndex, chars.Length);
        for (int i = 0; i < byteCount; i++) {
            byte b = bytes[byteIndex + i];
            if ((b >= 0x20 && b <= 0x7f) || b == 13 || b == 10) {
                chars[charIndex + i] = (char)b;
            } else {
                chars[charIndex + i] = '?';
            }
        }
        return byteCount;
    }

    public override int GetMaxByteCount(int charCount) {
        ArgumentOutOfRangeException.ThrowIfNegative(charCount);
        return charCount;
    }

    public override int GetMaxCharCount(int byteCount) {
        ArgumentOutOfRangeException.ThrowIfNegative(byteCount);
        return byteCount;
    }
}
