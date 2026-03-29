package io.github.kawase.packet.impl.game;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;
import java.util.ArrayList;
import java.util.List;

@Getter
public class FetchTasksResponsePacket extends Packet {
    public static class TaskDto {
        public long id;
        public String title;
        public int pointValue;

        public TaskDto(long id, String title, int pointValue) {
            this.id = id;
            this.title = title;
            this.pointValue = pointValue;
        }
    }

    private List<TaskDto> tasks = new ArrayList<>();

    public FetchTasksResponsePacket(final List<TaskDto> tasks) {
        super(0x0C);
        this.tasks = tasks;
    }

    public FetchTasksResponsePacket() {
        super(0x0C);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putInt(tasks.size());
        for (TaskDto task : tasks) {
            buffer.putLong(task.id);
            putString(task.title, buffer);
            buffer.putInt(task.pointValue);
        }
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        int size = buffer.getInt();
        for (int i = 0; i < size; i++) {
            long id = buffer.getLong();
            String title = readString(buffer);
            int points = buffer.getInt();
            tasks.add(new TaskDto(id, title, points));
        }
    }
}
