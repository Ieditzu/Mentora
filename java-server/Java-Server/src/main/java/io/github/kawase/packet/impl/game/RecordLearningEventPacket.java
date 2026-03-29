package io.github.kawase.packet.impl.game;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

@Getter
public class RecordLearningEventPacket extends Packet {
    private String eventType;
    private String topic;
    private int correctness;
    private String details;

    public RecordLearningEventPacket(final String eventType, final String topic, final int correctness, final String details) {
        super(33);
        this.eventType = eventType;
        this.topic = topic;
        this.correctness = correctness;
        this.details = details;
    }

    public RecordLearningEventPacket() {
        super(33);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(eventType == null ? "" : eventType, buffer);
        putString(topic == null ? "" : topic, buffer);
        buffer.putInt(correctness);
        putString(details == null ? "" : details, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.eventType = readString(buffer);
        this.topic = readString(buffer);
        this.correctness = buffer.getInt();
        this.details = readString(buffer);
    }
}
