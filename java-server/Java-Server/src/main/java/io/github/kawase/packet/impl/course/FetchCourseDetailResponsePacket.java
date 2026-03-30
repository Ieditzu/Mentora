package io.github.kawase.packet.impl.course;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class FetchCourseDetailResponsePacket extends Packet {
    private String courseJson;

    public FetchCourseDetailResponsePacket(final String courseJson) {
        super(39);
        this.courseJson = courseJson;
    }

    public FetchCourseDetailResponsePacket() {
        super(39);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(courseJson == null ? "{}" : courseJson, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.courseJson = readString(buffer);
    }
}
