package io.github.kawase.socket.packet.impl;

import io.github.kawase.socket.packet.Packet;

import java.nio.ByteBuffer;
import java.time.ZonedDateTime;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.List;

public class FetchCompletedTasksResponsePacket extends Packet {
    public static class CompletedTaskDto {
        public long id;
        public String taskTitle;
        public int pointValue;
        public String completedAt;

        public CompletedTaskDto(long id, String taskTitle, int pointValue, String completedAt) {
            this.id = id;
            this.taskTitle = taskTitle;
            this.pointValue = pointValue;
            this.completedAt = completedAt;
        }
    }

    private List<CompletedTaskDto> completedTasks = new ArrayList<>();

    public FetchCompletedTasksResponsePacket(final List<CompletedTaskDto> completedTasks) {
        super(0x12);
        this.completedTasks = completedTasks;
    }

    public FetchCompletedTasksResponsePacket() {
        super(0x12);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putInt(completedTasks.size());
        for (CompletedTaskDto ct : completedTasks) {
            buffer.putLong(ct.id);
            putString(ct.taskTitle, buffer);
            buffer.putInt(ct.pointValue);
            putString(ct.completedAt, buffer);
        }
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        int size = buffer.getInt();
        for (int i = 0; i < size; i++) {
            long id = buffer.getLong();
            String taskTitle = readString(buffer);
            int points = buffer.getInt();
            String at = readString(buffer);
            completedTasks.add(new CompletedTaskDto(id, taskTitle, points, at));
        }
    }

    public List<CompletedTaskDto> getCompletedTasks() {
        return completedTasks;
    }
}
