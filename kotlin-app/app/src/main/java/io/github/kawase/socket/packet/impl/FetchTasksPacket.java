package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class FetchTasksPacket extends Packet {

    public FetchTasksPacket() {
        super(0x0B);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
    }

    @Override
    protected void read(final ByteBuffer buffer) {
    }
}
