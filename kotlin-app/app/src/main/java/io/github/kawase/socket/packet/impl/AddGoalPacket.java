package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class AddGoalPacket extends Packet {
    private long childId;
    private String title;
    private String reward;
    private int requiredPoints;
    private long requiredTaskId;

    public AddGoalPacket(final long childId, final String title, final String reward, final int requiredPoints, final long requiredTaskId) {
        super(0x05);
        this.childId = childId;
        this.title = title;
        this.reward = reward;
        this.requiredPoints = requiredPoints;
        this.requiredTaskId = requiredTaskId;
    }

    public AddGoalPacket() {
        super(0x05);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
        putString(title, buffer);
        putString(reward, buffer);
        buffer.putInt(requiredPoints);
        buffer.putLong(requiredTaskId);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
        this.title = readString(buffer);
        this.reward = readString(buffer);
        this.requiredPoints = buffer.getInt();
        this.requiredTaskId = buffer.getLong();
    }

    public long getChildId() {
        return childId;
    }

    public String getTitle() {
        return title;
    }

    public String getReward() {
        return reward;
    }

    public int getRequiredPoints() {
        return requiredPoints;
    }

    public long getRequiredTaskId() {
        return requiredTaskId;
    }
}
