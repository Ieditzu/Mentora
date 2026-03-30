package io.github.kawase.packet.impl.course;

import io.github.kawase.packet.Packet;

import java.nio.ByteBuffer;

public class FetchPublishedCoursesPacket extends Packet {
    public FetchPublishedCoursesPacket() {
        super(36);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
    }

    @Override
    protected void read(final ByteBuffer buffer) {
    }
}
