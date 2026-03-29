package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class AddChildPacket extends Packet {
    private String childName;

    public AddChildPacket(final String childName) {
        super(0x04);
        this.childName = childName;
    }

    public AddChildPacket() {
        super(0x04);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(childName, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childName = readString(buffer);
    }

    public String getChildName() {
        return childName;
    }
}
