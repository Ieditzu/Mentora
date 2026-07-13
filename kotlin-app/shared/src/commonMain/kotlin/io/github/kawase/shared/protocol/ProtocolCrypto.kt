package io.github.kawase.shared.protocol

/**
 * Cryptographic primitives used by Mentora's existing socket protocol.
 *
 * The protocol derives an AES-256 key by SHA-256 hashing the UTF-8 password,
 * prefixes every ciphertext with its random 16-byte IV, and uses CBC with
 * PKCS#7 padding (PKCS5Padding in the JCA implementation).
 */
object ProtocolCrypto {
    private const val LONG_BYTE_COUNT = 8

    fun encryptLong(value: Long, key: String): ByteArray {
        return encryptBytes(longToBigEndianBytes(value), key)
    }

    fun decryptLong(encryptedData: ByteArray, key: String): Long {
        val bytes = decryptBytes(encryptedData, key)
        require(bytes.size == LONG_BYTE_COUNT) { "Decrypted data length mismatch." }

        var value = 0L
        for (byte in bytes) {
            value = (value shl 8) + (byte.toLong() and 0xFFL)
        }
        return value
    }

    fun encryptBytes(data: ByteArray, key: String): ByteArray {
        return PlatformCrypto.encryptAesCbcPkcs7(data, key)
    }

    fun decryptBytes(encryptedData: ByteArray, key: String): ByteArray {
        return PlatformCrypto.decryptAesCbcPkcs7(encryptedData, key)
    }

    internal fun nextDynamicSeed(): Long = PlatformCrypto.nanoTime()

    private fun longToBigEndianBytes(value: Long): ByteArray {
        return ByteArray(LONG_BYTE_COUNT) { index ->
            (value ushr ((LONG_BYTE_COUNT - 1 - index) * Byte.SIZE_BITS)).toByte()
        }
    }
}

/** Platform AES implementation plus the monotonic clock used for packet seeds. */
internal expect object PlatformCrypto {
    fun encryptAesCbcPkcs7(data: ByteArray, password: String): ByteArray
    fun decryptAesCbcPkcs7(encryptedData: ByteArray, password: String): ByteArray
    fun nanoTime(): Long
}
