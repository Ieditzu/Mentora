package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;


import java.nio.ByteBuffer;

public class HandShakePacket extends Packet {
    private String clientFingerPrint;

    public HandShakePacket(final String clientFingerPrint) {
        super(0x01);

        this.clientFingerPrint = clientFingerPrint;
    }

    public HandShakePacket() {
        super(0x01);
    }

    @Override
    public void write(final ByteBuffer buffer) {
        putString(clientFingerPrint, buffer);
    }

    @Override
    public void read(final ByteBuffer buffer) {
        this.clientFingerPrint = readString(buffer);
    }

    public String getClientFingerPrint() {
        return clientFingerPrint;
    }
}