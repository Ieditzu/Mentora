package io.github.kawase.socket.packet;

import io.github.kawase.socket.exceptions.PacketException;
import io.github.kawase.socket.interfaces.Data;
import io.github.kawase.socket.utility.EncryptionUtility;

import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;

/**
 * The parent packet class which every packet will extend
 * it uses AES to encrypt and decrypt and uses a basic dynamic key system
 * <p>
 * additional notes:
 * We could only write to the byte buffer encrypted data every time instead of encrypting it when sending it.
 * We could use a better encryption algorithm like ChaCha20 with poly 1305 and mac
 * We could also use Diffie-Hellman + a better dynamic key system for more security,
 * but none of that is rlly needed for a socket with the sole purpose of being used for an IRC.
 */
public abstract class Packet {
    private final int id;

    public Packet(final int id) {
        this.id = id;
    }

    public int getId() {
        return id;
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
            this.write(payloadBuffer);
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
            e.printStackTrace();
            throw new PacketException("Failed to encrypt: " + e.getMessage(), e);
        }
    }

    public void decode(final ByteBuffer byteBuffer) {
        this.read(byteBuffer);
    }

    /**
     * Constructs a packet object with the unencrypted data in it.
     * @param byteBuffer the buffer where the encrypted data is stored.
     * @param packetManager I wonder
     * @return the packet instance with the data in it
     * @throws Exception if the decryption fails.
     */
    public static Packet construct(final ByteBuffer byteBuffer, final PacketManager packetManager) throws Exception {
        final int seedLength = byteBuffer.getInt();

        // this should prevent out of memory attacks.
        if (seedLength <= 0 || seedLength > 1024) {
            throw new PacketException("Invalid seed length");
        }

        final byte[] encryptedSeed = new byte[seedLength];
        byteBuffer.get(encryptedSeed);

        final long dynamicSeed = EncryptionUtility.decryptLong(encryptedSeed, Data.baseKey);

        final byte[] encryptedPayload = new byte[byteBuffer.remaining()];
        byteBuffer.get(encryptedPayload);

        final byte[] decryptedPayloadBytes = EncryptionUtility.decryptBytes(encryptedPayload, String.valueOf(dynamicSeed));
        final ByteBuffer decryptedBuffer = ByteBuffer.wrap(decryptedPayloadBytes);

        final int packetID = decryptedBuffer.getInt();

        final Packet packet = packetManager.createPacket(packetID);
        packet.decode(decryptedBuffer);

        return packet;
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
        final int length = buffer.getInt();
        final byte[] bytes = new byte[length];

        buffer.get(bytes);

        return new String(bytes, StandardCharsets.UTF_8);
    }
}