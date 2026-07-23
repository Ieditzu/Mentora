package io.github.kawase.packet.impl.auth;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
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
}
