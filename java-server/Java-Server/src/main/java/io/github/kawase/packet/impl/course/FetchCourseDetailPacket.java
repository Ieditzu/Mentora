package io.github.kawase.packet.impl.course;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class FetchCourseDetailPacket extends Packet {
    private long courseId;

    public FetchCourseDetailPacket(final long courseId) {
        super(38);
        this.courseId = courseId;
    }

    public FetchCourseDetailPacket() {
        super(38);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(courseId);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.courseId = buffer.getLong();
    }
}
