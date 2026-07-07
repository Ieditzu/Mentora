package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class SubscribeLiveSessionPacket extends Packet {
    private long childId;
    private boolean subscribe;

    public SubscribeLiveSessionPacket(final long childId, final boolean subscribe) {
        super(64);
        this.childId = childId;
        this.subscribe = subscribe;
    }

    public SubscribeLiveSessionPacket() {
        super(64);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
        buffer.put((byte) (subscribe ? 1 : 0));
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
        this.subscribe = buffer.get() == 1;
    }

    public long getChildId() { return childId; }
    public boolean isSubscribe() { return subscribe; }
}
