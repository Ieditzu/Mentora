package io.github.kawase.packet.impl.course;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class FetchPublishedCoursesResponsePacket extends Packet {
    private String coursesJson;

    public FetchPublishedCoursesResponsePacket(final String coursesJson) {
        super(37);
        this.coursesJson = coursesJson;
    }

    public FetchPublishedCoursesResponsePacket() {
        super(37);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(coursesJson == null ? "[]" : coursesJson, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.coursesJson = readString(buffer);
    }
}
