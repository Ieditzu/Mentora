package io.github.kawase.packet;

import io.github.kawase.exceptions.PacketException;
import io.github.kawase.interfaces.Data;
import io.github.kawase.utility.EncryptionUtility;
import lombok.Getter;

import java.nio.ByteBuffer;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;

/**
 * The parent packet class which every packet will extend
 * it uses AES to encrypt and decrypt and uses a basic dynamic key system
 * <p>
 */
@Getter
public abstract class Packet {
    private static final int MAX_ENCRYPTED_PACKET_BYTES = 2 * 1024 * 1024;
    private static final int MAX_STRING_BYTES = 1024 * 1024;
    private static final int MIN_AES_CIPHERTEXT_BYTES = 32;
    private final int id;

    public Packet(final int id) {
        this.id = id;
    }

    /**
     * The children classes will override these methods to write / read their data into their fields.
     */
    protected abstract void write(final ByteBuffer buffer);
    protected abstract void read(final ByteBuffer buffer);

    public ByteBuffer encode() {
        try {
            final long dynamicSeed = System.nanoTime();

            final byte[] encryptedSeed = EncryptionUtility.encryptLong(dynamicSeed, Data.baseKey);
            // Increased to 1MB to handle large payloads like PFPs and histories
            final ByteBuffer payloadBuffer = ByteBuffer.allocate(1024 * 1024);

            payloadBuffer.putInt(id);
            write(payloadBuffer);
            payloadBuffer.flip();

            final byte[] payloadBytes = new byte[payloadBuffer.remaining()];
            payloadBuffer.get(payloadBytes);

            final byte[] encryptedPayload = EncryptionUtility.encryptBytes(payloadBytes, String.valueOf(dynamicSeed));
            final ByteBuffer finalBuffer = ByteBuffer.allocate(Integer.BYTES + encryptedSeed.length + encryptedPayload.length);

            finalBuffer.putInt(encryptedSeed.length);
            finalBuffer.put(encryptedSeed);
            finalBuffer.put(encryptedPayload);

            finalBuffer.flip();

            return finalBuffer;
        } catch (Exception e) {
            e.printStackTrace(); // Log full stack trace
            throw new PacketException("Failed to encrypt: " + e.getMessage(), e);
        }
    }

    public void decode(final ByteBuffer byteBuffer) {
        read(byteBuffer);
    }

    /**
     * Constructs a packet object with the unencrypted data in it.
     * @param byteBuffer the buffer where the encrypted data is stored.
     * @param packetManager I wonder
     * @return the packet instance with the data in it
     * @throws Exception if the decryption fails.
     */
    public static Packet construct(final ByteBuffer byteBuffer, final PacketManager packetManager) throws Exception {
        try {
            if (byteBuffer == null || byteBuffer.remaining() < Integer.BYTES)
                throw new PacketException("Packet frame is missing its seed length");
            if (byteBuffer.remaining() > MAX_ENCRYPTED_PACKET_BYTES)
                throw new PacketException("Packet frame exceeds the 2 MB limit");

            final int seedLength = byteBuffer.getInt();

            if (seedLength < MIN_AES_CIPHERTEXT_BYTES || seedLength > 1024 || seedLength > byteBuffer.remaining()
                    || (seedLength - 16) % 16 != 0) {
                throw new PacketException("Invalid seed length");
            }

            final byte[] encryptedSeed = new byte[seedLength];
            byteBuffer.get(encryptedSeed);

            final long dynamicSeed = EncryptionUtility.decryptLong(encryptedSeed, Data.baseKey);

            if (byteBuffer.remaining() < MIN_AES_CIPHERTEXT_BYTES || (byteBuffer.remaining() - 16) % 16 != 0)
                throw new PacketException("Invalid encrypted payload length");

            final byte[] encryptedPayload = new byte[byteBuffer.remaining()];
            byteBuffer.get(encryptedPayload);

            final byte[] decryptedPayloadBytes = EncryptionUtility.decryptBytes(encryptedPayload, String.valueOf(dynamicSeed));
            if (decryptedPayloadBytes.length < Integer.BYTES)
                throw new PacketException("Decrypted packet is missing its ID");
            final ByteBuffer decryptedBuffer = ByteBuffer.wrap(decryptedPayloadBytes);

            final int packetID = decryptedBuffer.getInt();

            final Packet packet = packetManager.createPacket(packetID);
            packet.decode(decryptedBuffer);
            if (decryptedBuffer.hasRemaining())
                throw new PacketException("Unexpected trailing packet data");

            return packet;
        } catch (PacketException exception) {
            throw exception;
        } catch (Exception exception) {
            throw new PacketException("Invalid encrypted packet", exception);
        }
    }

    /**
     * Helper for writing a string into a byte buffer.
     * @param data data to write
     * @param buffer the buffer to write the data to
     */
    public void putString(final String data, final ByteBuffer buffer) {
        final byte[] stringBytes = data.getBytes(StandardCharsets.UTF_8);

        buffer.putInt(stringBytes.length);
        buffer.put(stringBytes);
    }

    /**
     * Helper for reading a string from a buffer.
     * @param buffer the buffer to read from
     * @return the string it read from the buffer
     */
    public String readString(final ByteBuffer buffer) {
        if (buffer.remaining() < Integer.BYTES)
            throw new PacketException("String is missing its length");
        final int length = buffer.getInt();
        if (length < 0 || length > MAX_STRING_BYTES || length > buffer.remaining())
            throw new PacketException("Invalid string length");
        final byte[] bytes = new byte[length];

        buffer.get(bytes);

        try {
            return StandardCharsets.UTF_8.newDecoder()
                    .onMalformedInput(CodingErrorAction.REPORT)
                    .onUnmappableCharacter(CodingErrorAction.REPORT)
                    .decode(ByteBuffer.wrap(bytes))
                    .toString();
        } catch (CharacterCodingException exception) {
            throw new PacketException("String contains invalid UTF-8", exception);
        }
    }
}
