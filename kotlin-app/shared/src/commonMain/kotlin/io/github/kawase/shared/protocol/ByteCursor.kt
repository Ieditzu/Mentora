package io.github.kawase.shared.protocol

/** Thrown when an unencrypted Mentora packet payload is malformed or too large. */
class ProtocolException(message: String) : IllegalArgumentException(message)

/**
 * Small, dependency-free, big-endian byte reader/writer used by the shared protocol.
 *
 * The legacy Java client uses [java.nio.ByteBuffer]'s default big-endian order. Keeping
 * that detail here makes the Kotlin/Native codec byte-for-byte compatible without
 * relying on JVM-only APIs.
 */
class ByteCursor private constructor(
    private var bytes: ByteArray,
    private val writable: Boolean,
    private val maxSize: Int,
    private val maxStringBytes: Int
) {
    private var position = 0

    val remaining: Int get() = bytes.size - position
    val isAtEnd: Boolean get() = remaining == 0

    fun writeByte(value: Byte) {
        ensureWritable(1)
        bytes[position++] = value
    }

    fun writeBoolean(value: Boolean) = writeByte(if (value) 1 else 0)

    fun writeInt(value: Int) {
        ensureWritable(Int.SIZE_BYTES)
        bytes[position++] = (value ushr 24).toByte()
        bytes[position++] = (value ushr 16).toByte()
        bytes[position++] = (value ushr 8).toByte()
        bytes[position++] = value.toByte()
    }

    fun writeLong(value: Long) {
        ensureWritable(Long.SIZE_BYTES)
        for (shift in 56 downTo 0 step 8) bytes[position++] = (value ushr shift).toByte()
    }

    fun writeString(value: String) {
        val encoded = value.encodeToByteArray()
        if (encoded.size > maxStringBytes) {
            throw ProtocolException("String exceeds the $maxStringBytes-byte protocol limit")
        }
        writeInt(encoded.size)
        writeBytes(encoded)
    }

    fun readByte(): Byte {
        ensureReadable(1)
        return bytes[position++]
    }

    /** Matches the server/client convention: only byte value 1 represents true. */
    fun readBoolean(): Boolean = readByte().toInt() == 1

    fun readInt(): Int {
        ensureReadable(Int.SIZE_BYTES)
        return ((bytes[position++].toInt() and 0xff) shl 24) or
            ((bytes[position++].toInt() and 0xff) shl 16) or
            ((bytes[position++].toInt() and 0xff) shl 8) or
            (bytes[position++].toInt() and 0xff)
    }

    fun readLong(): Long {
        ensureReadable(Long.SIZE_BYTES)
        var value = 0L
        repeat(Long.SIZE_BYTES) { value = (value shl 8) or (bytes[position++].toLong() and 0xffL) }
        return value
    }

    fun readString(): String {
        val length = readInt()
        if (length < 0 || length > maxStringBytes || length > remaining) {
            throw ProtocolException("Invalid string length: $length")
        }
        val result = bytes.copyOfRange(position, position + length).decodeToString()
        position += length
        return result
    }

    fun readCollectionSize(maxElements: Int = DEFAULT_MAX_COLLECTION_ELEMENTS): Int {
        val size = readInt()
        if (size < 0 || size > maxElements) throw ProtocolException("Invalid collection size: $size")
        return size
    }

    fun toByteArray(): ByteArray {
        check(writable) { "A read cursor cannot be encoded" }
        return bytes.copyOf(position)
    }

    private fun writeBytes(value: ByteArray) {
        ensureWritable(value.size)
        value.copyInto(bytes, destinationOffset = position)
        position += value.size
    }

    private fun ensureWritable(count: Int) {
        check(writable) { "A read cursor cannot be written to" }
        if (count < 0 || position > maxSize - count) throw ProtocolException("Packet exceeds the $maxSize-byte limit")
        val needed = position + count
        if (needed <= bytes.size) return
        var newSize = bytes.size.coerceAtLeast(1)
        while (newSize < needed) newSize = (newSize * 2).coerceAtMost(maxSize)
        bytes = bytes.copyOf(newSize)
    }

    private fun ensureReadable(count: Int) {
        check(!writable) { "A write cursor cannot be read from" }
        if (count < 0 || count > remaining) throw ProtocolException("Packet ended unexpectedly")
    }

    companion object {
        const val DEFAULT_MAX_PACKET_BYTES = 1024 * 1024
        const val DEFAULT_MAX_STRING_BYTES = DEFAULT_MAX_PACKET_BYTES
        const val DEFAULT_MAX_COLLECTION_ELEMENTS = 10_000

        fun writer(
            maxSize: Int = DEFAULT_MAX_PACKET_BYTES,
            maxStringBytes: Int = DEFAULT_MAX_STRING_BYTES
        ): ByteCursor {
            require(maxSize > 0 && maxStringBytes in 0..maxSize)
            return ByteCursor(ByteArray(minOf(256, maxSize)), true, maxSize, maxStringBytes)
        }

        fun reader(
            payload: ByteArray,
            maxSize: Int = DEFAULT_MAX_PACKET_BYTES,
            maxStringBytes: Int = DEFAULT_MAX_STRING_BYTES
        ): ByteCursor {
            require(maxSize > 0 && maxStringBytes in 0..maxSize)
            if (payload.size > maxSize) throw ProtocolException("Packet exceeds the $maxSize-byte limit")
            return ByteCursor(payload, false, maxSize, maxStringBytes)
        }
    }
}
