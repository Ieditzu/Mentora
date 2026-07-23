package io.github.kawase.security;

import java.io.ByteArrayOutputStream;

final class Base32Codec {
    private static final char[] ALPHABET = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".toCharArray();

    private Base32Codec() {
        /* w */
    }

    static String encode(final byte[] bytes) {
        if (bytes == null || bytes.length == 0) return "";

        final StringBuilder encoded = new StringBuilder((bytes.length * 8 + 4) / 5);
        int storedBits = 0, storedBitCount = 0;
        for (final byte current : bytes) {
            storedBits = (storedBits << 8) | (current & 0xff);
            storedBitCount += 8;
            while (storedBitCount >= 5) {
                encoded.append(ALPHABET[(storedBits >> (storedBitCount - 5)) & 31]);
                storedBitCount -= 5;
            }
        }
        if (storedBitCount > 0)
            encoded.append(ALPHABET[(storedBits << (5 - storedBitCount)) & 31]);
        return encoded.toString();
    }

    static byte[] decode(final String encoded) {
        if (encoded == null || encoded.isBlank()) return new byte[0];

        final ByteArrayOutputStream decoded = new ByteArrayOutputStream(encoded.length() * 5 / 8);
        int storedBits = 0, storedBitCount = 0;
        for (final char current : encoded.toUpperCase().toCharArray()) {
            if (current == '=' || Character.isWhitespace(current) || current == '-') continue;

            final int value = decodeCharacter(current);
            if (value < 0)
                throw new IllegalArgumentException("Invalid Base32 character");
            storedBits = (storedBits << 5) | value;
            storedBitCount += 5;
            if (storedBitCount >= 8) {
                decoded.write((storedBits >> (storedBitCount - 8)) & 0xff);
                storedBitCount -= 8;
            }
        }
        return decoded.toByteArray();
    }

    private static int decodeCharacter(final char character) {
        if (character >= 'A' && character <= 'Z') return character - 'A';
        if (character >= '2' && character <= '7') return character - '2' + 26;
        return -1;
    }
}
