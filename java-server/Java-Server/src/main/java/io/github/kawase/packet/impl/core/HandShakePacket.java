package io.github.kawase.packet.impl.core;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
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
}