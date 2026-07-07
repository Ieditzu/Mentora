package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class SendParentChallengePacket extends Packet {
    private long childId;
    private String message;

    public SendParentChallengePacket(final long childId, final String message) {
        super(66);
        this.childId = childId;
        this.message = message;
    }

    public SendParentChallengePacket() {
        super(66);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
        putString(message == null ? "" : message, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
        this.message = readString(buffer);
    }

    public long getChildId() { return childId; }
    public String getMessage() { return message; }
}
