package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;
import java.nio.ByteBuffer;

public class FetchChildStatsResponsePacket extends Packet {
    private String name;
    private int totalPoints;
    private String gameStatsJson;
    private int streak;
    private int completedTaskCount;
    private int totalTaskCount;

    public FetchChildStatsResponsePacket(final String name, final int totalPoints, final String gameStatsJson,
                                          final int streak, final int completedTaskCount, final int totalTaskCount) {
        super(24);
        this.name = name;
        this.totalPoints = totalPoints;
        this.gameStatsJson = gameStatsJson;
        this.streak = streak;
        this.completedTaskCount = completedTaskCount;
        this.totalTaskCount = totalTaskCount;
    }

    public FetchChildStatsResponsePacket() {
        super(24);
    }

    public String getName() { return name; }
    public int getTotalPoints() { return totalPoints; }
    public String getGameStatsJson() { return gameStatsJson; }
    public int getStreak() { return streak; }
    public int getCompletedTaskCount() { return completedTaskCount; }
    public int getTotalTaskCount() { return totalTaskCount; }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(name == null ? "" : name, buffer);
        buffer.putInt(totalPoints);
        putString(gameStatsJson == null ? "{}" : gameStatsJson, buffer);
        buffer.putInt(streak);
        buffer.putInt(completedTaskCount);
        buffer.putInt(totalTaskCount);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.name = readString(buffer);
        this.totalPoints = buffer.getInt();
        this.gameStatsJson = readString(buffer);
        this.streak = buffer.getInt();
        this.completedTaskCount = buffer.getInt();
        this.totalTaskCount = buffer.getInt();
    }
}
