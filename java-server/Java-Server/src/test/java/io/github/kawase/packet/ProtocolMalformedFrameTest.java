package io.github.kawase.packet;

import io.github.kawase.exceptions.PacketException;
import io.github.kawase.interfaces.Data;
import io.github.kawase.utility.EncryptionUtility;
import org.junit.jupiter.api.Test;

import java.nio.ByteBuffer;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

class ProtocolMalformedFrameTest {
    private final PacketManager packetManager = new PacketManager();

    @Test
    void rejectsMissingInvalidAndTruncatedEncryptedSegments() {
        assertThrows(PacketException.class, () -> Packet.construct(ByteBuffer.allocate(3), packetManager));
        assertThrows(PacketException.class, () -> Packet.construct(ByteBuffer.allocate(2 * 1024 * 1024 + 1), packetManager));
        assertThrows(PacketException.class, () -> Packet.construct(ByteBuffer.allocate(4).putInt(0).flip(), packetManager));
        assertThrows(PacketException.class, () -> Packet.construct(ByteBuffer.allocate(4).putInt(1025).flip(), packetManager));
        assertThrows(PacketException.class, () -> Packet.construct(
                ByteBuffer.allocate(4 + 16).putInt(32).put(new byte[16]).flip(),
                packetManager
        ));
        final PacketException invalidPayloadLength = assertThrows(
                PacketException.class,
                () -> Packet.construct(frameWithEncryptedPayload(new byte[16]), packetManager)
        );
        assertEquals("Invalid encrypted payload length", invalidPayloadLength.getMessage());
    }

    @Test
    void rejectsUnknownPacketAndMalformedUtf8LengthsAfterValidDecryption() throws Exception {
        assertThrows(PacketException.class, () -> Packet.construct(
                encryptPayload(ByteBuffer.allocate(4).putInt(999).array()),
                packetManager
        ));
        assertThrows(PacketException.class, () -> Packet.construct(
                encryptPayload(ByteBuffer.allocate(8).putInt(2).putInt(-1).array()),
                packetManager
        ));
        assertThrows(PacketException.class, () -> Packet.construct(
                encryptPayload(ByteBuffer.allocate(11).putInt(2).putInt(12).put(new byte[3]).array()),
                packetManager
        ));
        assertThrows(PacketException.class, () -> Packet.construct(
                encryptPayload(ByteBuffer.allocate(10)
                        .putInt(2)
                        .putInt(2)
                        .put(new byte[] { (byte) 0xC3, 0x28 })
                        .array()),
                packetManager
        ));
        final PacketException trailingData = assertThrows(
                PacketException.class,
                () -> Packet.construct(
                        encryptPayload(ByteBuffer.allocate(5).putInt(88).put((byte) 1).array()),
                        packetManager
                )
        );
        assertEquals("Unexpected trailing packet data", trailingData.getMessage());
    }

    private ByteBuffer encryptPayload(final byte[] payload) throws Exception {
        final long seed = 1_700_000_000_123_456_789L;
        final byte[] encryptedSeed = EncryptionUtility.encryptLong(seed, Data.baseKey);
        final byte[] encryptedPayload = EncryptionUtility.encryptBytes(payload, Long.toString(seed));
        return ByteBuffer.allocate(Integer.BYTES + encryptedSeed.length + encryptedPayload.length)
                .putInt(encryptedSeed.length)
                .put(encryptedSeed)
                .put(encryptedPayload)
                .flip();
    }

    private ByteBuffer frameWithEncryptedPayload(final byte[] encryptedPayload) throws Exception {
        final long seed = 1_700_000_000_123_456_789L;
        final byte[] encryptedSeed = EncryptionUtility.encryptLong(seed, Data.baseKey);
        return ByteBuffer.allocate(Integer.BYTES + encryptedSeed.length + encryptedPayload.length)
                .putInt(encryptedSeed.length)
                .put(encryptedSeed)
                .put(encryptedPayload)
                .flip();
    }
}
