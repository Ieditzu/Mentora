package io.github.kawase.packet.impl.child;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class FetchProgrammingProfileSummaryResponsePacket extends Packet {
    private long childId;
    private String childName;
    private int totalPoints;
    private int streak;
    private int completedTaskCount;
    private int totalTaskCount;
    private String profileSummary;

    public FetchProgrammingProfileSummaryResponsePacket(final long childId,
                                                        final String childName,
                                                        final int totalPoints,
                                                        final int streak,
                                                        final int completedTaskCount,
                                                        final int totalTaskCount,
                                                        final String profileSummary) {
        super(72);
        this.childId = childId;
        this.childName = childName;
        this.totalPoints = totalPoints;
        this.streak = streak;
        this.completedTaskCount = completedTaskCount;
        this.totalTaskCount = totalTaskCount;
        this.profileSummary = profileSummary;
    }

    public FetchProgrammingProfileSummaryResponsePacket() {
        super(72);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
        putString(childName == null ? "" : childName, buffer);
        buffer.putInt(totalPoints);
        buffer.putInt(streak);
        buffer.putInt(completedTaskCount);
        buffer.putInt(totalTaskCount);
        putString(profileSummary == null ? "" : profileSummary, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
        this.childName = readString(buffer);
        this.totalPoints = buffer.getInt();
        this.streak = buffer.getInt();
        this.completedTaskCount = buffer.getInt();
        this.totalTaskCount = buffer.getInt();
        this.profileSummary = readString(buffer);
    }
}
