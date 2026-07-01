package io.github.kawase.packet.impl.companion;

import io.github.kawase.packet.Packet;
import lombok.Getter;

import java.nio.ByteBuffer;

@Getter
public class CompanionVoiceAudioPacket extends Packet {
    private static final int MAX_AUDIO_BYTES = 512 * 1024;

    private int sampleRate;
    private byte[] pcm16;
    private String context;

    public CompanionVoiceAudioPacket(final int sampleRate, final byte[] pcm16, final String context) {
        super(59);
        this.sampleRate = sampleRate;
        this.pcm16 = pcm16 == null ? new byte[0] : pcm16;
        this.context = context;
    }

    public CompanionVoiceAudioPacket() {
        super(59);
    }

    @Override
    protected void write(final ByteBuffer buffer) {
        buffer.putInt(sampleRate);
        byte[] data = pcm16 == null ? new byte[0] : pcm16;
        buffer.putInt(data.length);
        buffer.put(data);
        putString(context == null ? "" : context, buffer);
    }

    @Override
    protected void read(final ByteBuffer buffer) {
        this.sampleRate = buffer.getInt();
        int length = buffer.getInt();
        if (length < 0 || length > MAX_AUDIO_BYTES || length > buffer.remaining()) {
            this.pcm16 = new byte[0];
            this.context = "";
            return;
        }

        this.pcm16 = new byte[length];
        buffer.get(this.pcm16);
        this.context = buffer.hasRemaining() ? readString(buffer) : "";
    }
}
