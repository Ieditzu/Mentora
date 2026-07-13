package io.github.kawase.shared.protocol

/**
 * Encodes and decodes the encrypted outer packet frame used by the Java server.
 *
 * Frame layout, in network byte order:
 * `seedCiphertextLength (Int) | encryptedSeed | encryptedPayload`.
 * The payload is deliberately opaque here; packet-specific code owns its bytes.
 */
class PacketFrameCodec(private val baseKey: String = DEFAULT_BASE_KEY) {
    fun encode(payload: ByteArray): ByteArray = encode(payload, ProtocolCrypto.nextDynamicSeed())

    /** Exposed for deterministic protocol tests; production callers should use [encode]. */
    fun encode(payload: ByteArray, dynamicSeed: Long): ByteArray {
        val encryptedSeed = ProtocolCrypto.encryptLong(dynamicSeed, baseKey)
        val encryptedPayload = ProtocolCrypto.encryptBytes(payload, dynamicSeed.toString())
        return ByteArray(Int.SIZE_BYTES + encryptedSeed.size + encryptedPayload.size).also { frame ->
            writeIntBigEndian(encryptedSeed.size, frame, 0)
            encryptedSeed.copyInto(frame, destinationOffset = Int.SIZE_BYTES)
            encryptedPayload.copyInto(frame, destinationOffset = Int.SIZE_BYTES + encryptedSeed.size)
        }
    }

    fun decode(frame: ByteArray): ByteArray {
        require(frame.size >= Int.SIZE_BYTES) { "Packet frame is missing its seed length." }

        val encryptedSeedLength = readIntBigEndian(frame, 0)
        require(encryptedSeedLength in 1..MAX_ENCRYPTED_SEED_LENGTH) { "Invalid seed length" }
        require(frame.size >= Int.SIZE_BYTES + encryptedSeedLength) { "Packet frame is truncated." }

        val encryptedSeedStart = Int.SIZE_BYTES
        val encryptedPayloadStart = encryptedSeedStart + encryptedSeedLength
        val encryptedSeed = frame.copyOfRange(encryptedSeedStart, encryptedPayloadStart)
        val dynamicSeed = ProtocolCrypto.decryptLong(encryptedSeed, baseKey)
        val encryptedPayload = frame.copyOfRange(encryptedPayloadStart, frame.size)
        return ProtocolCrypto.decryptBytes(encryptedPayload, dynamicSeed.toString())
    }

    private fun writeIntBigEndian(value: Int, target: ByteArray, offset: Int) {
        target[offset] = (value ushr 24).toByte()
        target[offset + 1] = (value ushr 16).toByte()
        target[offset + 2] = (value ushr 8).toByte()
        target[offset + 3] = value.toByte()
    }

    private fun readIntBigEndian(source: ByteArray, offset: Int): Int {
        return ((source[offset].toInt() and 0xFF) shl 24) or
            ((source[offset + 1].toInt() and 0xFF) shl 16) or
            ((source[offset + 2].toInt() and 0xFF) shl 8) or
            (source[offset + 3].toInt() and 0xFF)
    }

    companion object {
        // Kept for wire compatibility with the deployed Java server.
        const val DEFAULT_BASE_KEY = "CIOCLIKESKIDSIJIJSDJ1J2313J8123869699696"
        private const val MAX_ENCRYPTED_SEED_LENGTH = 1024
    }
}
