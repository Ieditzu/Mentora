package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class ParentAuthSessionPacket extends Packet {
    private boolean success;
    private long parentId, expiresAtEpochSeconds;
    private String message, parentPfp, sessionToken;

    public ParentAuthSessionPacket(
            final boolean success,
            final long parentId,
            final String message,
            final String parentPfp,
            final String sessionToken,
            final long expiresAtEpochSeconds) {
        super(90);
        this.success = success;
        this.parentId = parentId;
        this.message = message;
        this.parentPfp = parentPfp;
        this.sessionToken = sessionToken;
        this.expiresAtEpochSeconds = expiresAtEpochSeconds;
    }

    public ParentAuthSessionPacket() {
        super(90);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.put((byte) (success ? 1 : 0));
        buffer.putLong(parentId);
        putString(message == null ? "" : message, buffer);
        putString(parentPfp == null ? "" : parentPfp, buffer);
        putString(sessionToken == null ? "" : sessionToken, buffer);
        buffer.putLong(expiresAtEpochSeconds);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        success = buffer.get() == 1;
        parentId = buffer.getLong();
        message = readString(buffer);
        parentPfp = readString(buffer);
        sessionToken = readString(buffer);
        expiresAtEpochSeconds = buffer.getLong();
    }

    public boolean isSuccess() {
        return success;
    }

    public long getParentId() {
        return parentId;
    }

    public String getMessage() {
        return message;
    }

    public String getParentPfp() {
        return parentPfp;
    }

    public String getSessionToken() {
        return sessionToken;
    }

    public long getExpiresAtEpochSeconds() {
        return expiresAtEpochSeconds;
    }
}
