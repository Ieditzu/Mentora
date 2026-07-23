package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class FetchParentSecurityStatusPacket extends Packet {
    public FetchParentSecurityStatusPacket() {
        super(88);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        /* w */
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        /* w */
    }
}
