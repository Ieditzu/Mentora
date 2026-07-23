package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class ResumeParentSessionPacket extends Packet {
    private String sessionToken, deviceId;

    public ResumeParentSessionPacket(final String sessionToken, final String deviceId) {
        super(91);
        this.sessionToken = sessionToken;
        this.deviceId = deviceId;
    }

    public ResumeParentSessionPacket() {
        super(91);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(sessionToken == null ? "" : sessionToken, buffer);
        putString(deviceId == null ? "" : deviceId, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        sessionToken = readString(buffer);
        deviceId = readString(buffer);
    }

    public String getSessionToken() {
        return sessionToken;
    }

    public String getDeviceId() {
        return deviceId;
    }
}
