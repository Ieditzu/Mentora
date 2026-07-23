package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class HandShakePacket extends Packet {
    private String clientFingerPrint, deviceId;
    private int protocolVersion;

    public HandShakePacket(final String clientFingerPrint) {
        super(0x01);
        this.clientFingerPrint = clientFingerPrint;
        protocolVersion = 1;
        deviceId = "";
    }

    public HandShakePacket(final String clientFingerPrint, final int protocolVersion, final String deviceId) {
        super(0x01);
        this.clientFingerPrint = clientFingerPrint;
        this.protocolVersion = Math.max(1, protocolVersion);
        this.deviceId = deviceId == null ? "" : deviceId;
    }

    public HandShakePacket() {
        super(0x01);
        protocolVersion = 1;
        deviceId = "";
    }

    @Override
    public void write(final ByteBuffer buffer) {
        putString(clientFingerPrint, buffer);
        if (protocolVersion >= 2 || !deviceId.isBlank()) {
            buffer.putInt(protocolVersion);
            putString(deviceId, buffer);
        }
    }

    @Override
    public void read(final ByteBuffer buffer) {
        clientFingerPrint = readString(buffer);
        if (buffer.remaining() >= Integer.BYTES)
            protocolVersion = Math.max(1, buffer.getInt());
        if (buffer.hasRemaining())
            deviceId = readString(buffer);
    }

    public String getClientFingerPrint() {
        return clientFingerPrint;
    }

    public int getProtocolVersion() {
        return protocolVersion;
    }

    public String getDeviceId() {
        return deviceId;
    }
}
