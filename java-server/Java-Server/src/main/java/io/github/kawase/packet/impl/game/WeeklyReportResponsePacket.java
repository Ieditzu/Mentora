package io.github.kawase.packet.impl.game;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class WeeklyReportResponsePacket extends Packet {
    private long childId;
    private String childName;
    private String weekStart;
    private String weekEnd;
    private String reportText;
    private boolean aiGenerated;

    public WeeklyReportResponsePacket(final long childId, final String childName, final String weekStart,
                                      final String weekEnd, final String reportText, final boolean aiGenerated) {
        super(70);
        this.childId = childId;
        this.childName = childName;
        this.weekStart = weekStart;
        this.weekEnd = weekEnd;
        this.reportText = reportText;
        this.aiGenerated = aiGenerated;
    }

    public WeeklyReportResponsePacket() {
        super(70);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
        putString(childName == null ? "" : childName, buffer);
        putString(weekStart == null ? "" : weekStart, buffer);
        putString(weekEnd == null ? "" : weekEnd, buffer);
        putString(reportText == null ? "" : reportText, buffer);
        buffer.put((byte) (aiGenerated ? 1 : 0));
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
        this.childName = readString(buffer);
        this.weekStart = readString(buffer);
        this.weekEnd = readString(buffer);
        this.reportText = readString(buffer);
        this.aiGenerated = buffer.get() == 1;
    }
}
