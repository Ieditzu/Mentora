package io.github.kawase.packet.impl.auth;

import io.github.kawase.packet.Packet;

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
