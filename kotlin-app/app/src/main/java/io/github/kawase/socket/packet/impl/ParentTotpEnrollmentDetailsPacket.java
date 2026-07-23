package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class ParentTotpEnrollmentDetailsPacket extends Packet {
    private String enrollmentId, secretBase32, otpAuthUri;

    public ParentTotpEnrollmentDetailsPacket(
            final String enrollmentId,
            final String secretBase32,
            final String otpAuthUri) {
        super(84);
        this.enrollmentId = enrollmentId;
        this.secretBase32 = secretBase32;
        this.otpAuthUri = otpAuthUri;
    }

    public ParentTotpEnrollmentDetailsPacket() {
        super(84);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(enrollmentId == null ? "" : enrollmentId, buffer);
        putString(secretBase32 == null ? "" : secretBase32, buffer);
        putString(otpAuthUri == null ? "" : otpAuthUri, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        enrollmentId = readString(buffer);
        secretBase32 = readString(buffer);
        otpAuthUri = readString(buffer);
    }

    public String getEnrollmentId() {
        return enrollmentId;
    }

    public String getSecretBase32() {
        return secretBase32;
    }

    public String getOtpAuthUri() {
        return otpAuthUri;
    }
}
