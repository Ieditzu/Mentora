package io.github.kawase.packet.impl.course;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class SubmitCourseCompletionPacket extends Packet {
    private long courseId;
    private int score;
    private int totalQuestions;

    public SubmitCourseCompletionPacket(final long courseId, final int score, final int totalQuestions) {
        super(40);
        this.courseId = courseId;
        this.score = score;
        this.totalQuestions = totalQuestions;
    }

    public SubmitCourseCompletionPacket() {
        super(40);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(courseId);
        buffer.putInt(score);
        buffer.putInt(totalQuestions);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.courseId = buffer.getLong();
        this.score = buffer.getInt();
        this.totalQuestions = buffer.getInt();
    }
}
