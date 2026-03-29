package io.github.kawase.packet.impl.core;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class ActionResponsePacket extends Packet {
    private int requestPacketId;
    private boolean success;
    private String message;
    private long resultId;

    public ActionResponsePacket(final int requestPacketId, final boolean success, final String message, final long resultId) {
        super(0x09);
        this.requestPacketId = requestPacketId;
        this.success = success;
        this.message = message;
        this.resultId = resultId;
    }

    public ActionResponsePacket() {
        super(0x09);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putInt(requestPacketId);
        buffer.put((byte) (success ? 1 : 0));
        putString(message == null ? "" : message, buffer);
        buffer.putLong(resultId);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.requestPacketId = buffer.getInt();
        this.success = buffer.get() == 1;
        this.message = readString(buffer);
        this.resultId = buffer.getLong();
    }
}
