package io.github.kawase.packet.impl.core;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class HandShakePacket extends Packet {
    private String clientFingerPrint;
    private int protocolVersion = 1;
    private String deviceId = "";

    public HandShakePacket(final String clientFingerPrint) {
        super(0x01);
        this.clientFingerPrint = clientFingerPrint;
    }

    public HandShakePacket(final String clientFingerPrint, final int protocolVersion, final String deviceId) {
        super(0x01);
        this.clientFingerPrint = clientFingerPrint;
        this.protocolVersion = Math.max(1, protocolVersion);
        this.deviceId = deviceId == null ? "" : deviceId;
    }

    public HandShakePacket() {
        super(0x01);
    }

    @Override
    public void write(final ByteBuffer buffer) {
        putString(clientFingerPrint, buffer);
        if (protocolVersion <= 1 && deviceId.isBlank()) return;

        buffer.putInt(protocolVersion);
        putString(deviceId, buffer);
    }

    @Override
    public void read(final ByteBuffer buffer) {
        clientFingerPrint = readString(buffer);
        if (buffer.remaining() < Integer.BYTES) return;

        protocolVersion = Math.max(1, buffer.getInt());
        if (buffer.hasRemaining())
            deviceId = readString(buffer);
    }
}
