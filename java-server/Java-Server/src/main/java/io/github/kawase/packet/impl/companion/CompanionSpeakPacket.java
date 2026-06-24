package io.github.kawase.packet.impl.companion;

import io.github.kawase.packet.Packet;
import lombok.Getter;
import java.nio.ByteBuffer;

/**
 * Sent by Unity when the robot companion should say something.
 * The trigger describes the context: "greet", "challenge_fail",
 * "challenge_success", "idle", "entering_python", "entering_cpp",
 * "entering_community", "task_complete", "hint_requested".
 */
@Getter
public class CompanionSpeakPacket extends Packet {
    private String trigger;

    public CompanionSpeakPacket(final String trigger) {
        super(47);
        this.trigger = trigger;
    }

    public CompanionSpeakPacket() {
        super(47);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        putString(trigger == null ? "idle" : trigger, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.trigger = readString(buffer);
    }
}
