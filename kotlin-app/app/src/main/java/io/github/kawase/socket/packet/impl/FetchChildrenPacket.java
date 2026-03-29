package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class FetchChildrenPacket extends Packet {
    public FetchChildrenPacket() {
        super(0x0F);
    }

    @Override
    protected void write(final ByteBuffer buffer) {}

    @Override
    protected void read(final ByteBuffer buffer) {}
}
