package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;

public class CompleteTaskPacket extends Packet {
    private long childId;
    private long taskId;

    public CompleteTaskPacket(final long childId, final long taskId) {
        super(0x08);
        this.childId = childId;
        this.taskId = taskId;
    }

    public CompleteTaskPacket() {
        super(0x08);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putLong(childId);
        buffer.putLong(taskId);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.childId = buffer.getLong();
        this.taskId = buffer.getLong();
    }

    public long getChildId() {
        return childId;
    }

    public long getTaskId() {
        return taskId;
    }
}
