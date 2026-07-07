package io.github.kawase.packet.impl.game;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class FetchWeeklyReportPacket extends Packet {
    private long childId;

    public FetchWeeklyReportPacket(final long childId) {
        super(69);
        this.childId = childId;
    }

    public FetchWeeklyReportPacket() {
        super(69);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
    }
}
