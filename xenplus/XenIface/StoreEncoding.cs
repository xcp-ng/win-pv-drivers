using System.Text;

namespace XenPlus.XenIface;

sealed class StoreEncoding : Encoding {
    public static StoreEncoding Instance = new();

    public override int GetByteCount(char[] chars, int index, int count) {
        return count;
    }

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
        for (int i = 0; i < charCount; i++) {
            char c = chars[charIndex + i];
            if (c >= '\x20' && c <= '\x7f') {
                bytes[byteIndex + i] = (byte)c;
            } else {
                bytes[byteIndex + i] = (byte)'?';
            }
        }
        return charCount;
    }

    public override int GetCharCount(byte[] bytes, int index, int count) {
        return count;
    }

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
        for (int i = 0; i < byteCount; i++) {
            byte b = bytes[byteIndex + i];
            if (b >= 0x20 && b <= 0x7f) {
                chars[charIndex + i] = (char)b;
            } else {
                chars[charIndex + i] = '?';
            }
        }
        return byteCount;
    }

    public override int GetMaxByteCount(int charCount) {
        return charCount;
    }

    public override int GetMaxCharCount(int byteCount) {
        return byteCount;
    }
}
