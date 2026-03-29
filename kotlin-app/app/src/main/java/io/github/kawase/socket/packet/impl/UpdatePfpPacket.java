package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;
import java.nio.ByteBuffer;

public class UpdatePfpPacket extends Packet {
    private long childId;
    private String base64Pfp;

    public UpdatePfpPacket(final long childId, final String base64Pfp) {
        super(26);
        this.childId = childId;
        this.base64Pfp = base64Pfp;
    }

    public UpdatePfpPacket() {
        super(26);
    }

    public long getChildId() {
        return childId;
    }

    public String getBase64Pfp() {
        return base64Pfp;
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
        putString(base64Pfp == null ? "" : base64Pfp, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
        this.base64Pfp = readString(buffer);
    }
}
