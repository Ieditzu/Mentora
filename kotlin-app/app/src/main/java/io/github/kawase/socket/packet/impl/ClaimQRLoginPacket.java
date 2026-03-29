package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;
import java.nio.ByteBuffer;

public class ClaimQRLoginPacket extends Packet {
    private String token;
    private long childId;

    public ClaimQRLoginPacket(final String token, final long childId) {
        super(21);
        this.token = token;
        this.childId = childId;
    }

    public ClaimQRLoginPacket() {
        super(21);
    }

    public String getToken() {
        return token;
    }

    public long getChildId() {
        return childId;
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(token == null ? "" : token, buffer);
        buffer.putLong(childId);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.token = readString(buffer);
        this.childId = buffer.getLong();
    }
}
