package io.github.kawase.packet.impl.ai;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class GenerateAiTaskResponsePacket extends Packet {
    private long taskId;
    private String title;
    private String description;
    private String codeTemplate;
    private String language;
    private int pointValue;

    public GenerateAiTaskResponsePacket(final long taskId, final String title, final String description,
                                         final String codeTemplate, final String language, final int pointValue) {
        super(46);
        this.taskId = taskId;
        this.title = title;
        this.description = description;
        this.codeTemplate = codeTemplate;
        this.language = language;
        this.pointValue = pointValue;
    }

    public GenerateAiTaskResponsePacket() {
        super(46);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(taskId);
        putString(title == null ? "" : title, buffer);
        putString(description == null ? "" : description, buffer);
        putString(codeTemplate == null ? "" : codeTemplate, buffer);
        putString(language == null ? "" : language, buffer);
        buffer.putInt(pointValue);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.taskId = buffer.getLong();
        this.title = readString(buffer);
        this.description = readString(buffer);
        this.codeTemplate = readString(buffer);
        this.language = readString(buffer);
        this.pointValue = buffer.getInt();
    }
}
