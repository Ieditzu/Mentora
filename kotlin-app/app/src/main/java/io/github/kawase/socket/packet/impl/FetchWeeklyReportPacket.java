package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class FetchWeeklyReportPacket extends Packet {
    private long childId;

    public FetchWeeklyReportPacket(final long childId) {
        super(69);
        this.childId = childId;
    }

    public FetchWeeklyReportPacket() {
        super(69);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
    }

    public long getChildId() { return childId; }
}
