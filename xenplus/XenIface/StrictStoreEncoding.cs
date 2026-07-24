using System.Runtime.CompilerServices;
using System.Text;

namespace XenPlus.XenIface;

sealed class StrictStoreEncoding : Encoding {
    public static StrictStoreEncoding Instance = new();

    static unsafe void CheckBuffer(
        void* buffer,
        int length,
        [CallerArgumentExpression(nameof(buffer))] string name = "") {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentNullException.ThrowIfNull(buffer, name);
    }

    public override unsafe int GetByteCount(char* chars, int count) {
        CheckBuffer(chars, count);
        for (int i = 0; i < count; i++) {
            char c = chars[i];
            if (!((c >= '\x20' && c <= '\x7f') || c == '\n')) {
                throw new EncoderFallbackException("found out-of-range char");
            }
        }
        return count;
    }

    public override int GetByteCount(char[] chars, int index, int count) {
        ArgumentNullException.ThrowIfNull(chars);
        return GetByteCount(chars.AsSpan(index, count));
    }

    public override unsafe int GetBytes(char* chars, int charCount, byte* bytes, int byteCount) {
        CheckBuffer(chars, charCount);
        CheckBuffer(bytes, byteCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(byteCount, charCount);
        for (int i = 0; i < charCount; i++) {
            char c = chars[i];
            if (!((c >= '\x20' && c <= '\x7f') || c == '\n')) {
                throw new EncoderFallbackException("found out-of-range char");
            }
            bytes[i] = (byte)c;
        }
        return charCount;
    }

    public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex) {
        ArgumentNullException.ThrowIfNull(bytes);
        var destination = bytes.AsSpan(byteIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            destination.Length,
            GetByteCount(chars, charIndex, charCount),
            nameof(bytes));
        return GetBytes(chars.AsSpan(charIndex, charCount), destination);
    }

    public override unsafe int GetCharCount(byte* bytes, int count) {
        CheckBuffer(bytes, count);
        for (int i = 0; i < count; i++) {
            byte b = bytes[i];
            if (!((b >= 0x20 && b <= 0x7f) || b == 10)) {
                throw new DecoderFallbackException("found out-of-range byte");
            }
        }
        return count;
    }

    public override int GetCharCount(byte[] bytes, int index, int count) {
        ArgumentNullException.ThrowIfNull(bytes);
        return GetCharCount(bytes.AsSpan(index, count));
    }

    public override unsafe int GetChars(byte* bytes, int byteCount, char* chars, int charCount) {
        CheckBuffer(bytes, byteCount);
        CheckBuffer(chars, charCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(charCount, byteCount);
        for (int i = 0; i < byteCount; i++) {
            byte b = bytes[i];
            if (!((b >= 0x20 && b <= 0x7f) || b == 10)) {
                throw new DecoderFallbackException("found out-of-range byte");
            }
            chars[i] = (char)b;
        }
        return byteCount;
    }

    public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex) {
        ArgumentNullException.ThrowIfNull(chars);
        var destination = chars.AsSpan(charIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            destination.Length,
            GetCharCount(bytes, byteIndex, byteCount),
            nameof(chars));
        return GetChars(bytes.AsSpan(byteIndex, byteCount), destination);
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
