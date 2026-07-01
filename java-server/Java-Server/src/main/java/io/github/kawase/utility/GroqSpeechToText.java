package io.github.kawase.utility;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;

public class GroqSpeechToText {
    private static final String TRANSCRIPTION_URL = "https://api.groq.com/openai/v1/audio/transcriptions";
    private static final String MODEL = "whisper-large-v3-turbo";
    private static final String PROMPT = "Mentora game voice. Important words: Rudolf, Python Island, C++ Island, C plus plus Island, Logic Island, Community Island, code, run, execute, debug.";
    private static final int MIN_SAMPLE_RATE = 8000;
    private static final int MAX_SAMPLE_RATE = 48000;

    public String transcribePcm16(final byte[] pcm16, final int sampleRate) {
        if (pcm16 == null || pcm16.length < 2) {
            return "";
        }

        final String apiKey = GroqAI.getConfiguredApiKey();
        if (apiKey == null || apiKey.isBlank()) {
            System.out.println("[GroqSTT] No Groq API key configured.");
            return "";
        }

        final int resolvedSampleRate = Math.max(MIN_SAMPLE_RATE, Math.min(MAX_SAMPLE_RATE, sampleRate));
        final byte[] wavBytes = buildWavBytes(pcm16, resolvedSampleRate);
        try {
            final String boundary = "MentoraGroqBoundary" + System.nanoTime();
            final byte[] body = buildMultipartBody(boundary, wavBytes);
            final HttpRequest request = HttpRequest.newBuilder()
                    .uri(URI.create(TRANSCRIPTION_URL))
                    .header("Authorization", "Bearer " + apiKey)
                    .header("Content-Type", "multipart/form-data; boundary=" + boundary)
                    .POST(HttpRequest.BodyPublishers.ofByteArray(body))
                    .build();

            final HttpResponse<String> response = HttpClient.newHttpClient()
                    .send(request, HttpResponse.BodyHandlers.ofString());
            if (response.statusCode() != 200) {
                System.out.println("[GroqSTT] Error " + response.statusCode() + ": " + response.body());
                return "";
            }

            final JSONObject json = new JSONObject(response.body());
            final String text = json.optString("text", "");
            System.out.println("[GroqSTT] Transcript=" + text);
            return text == null ? "" : text.trim();
        } catch (Exception e) {
            System.out.println("[GroqSTT] Exception: " + e.getClass().getName());
            e.printStackTrace();
            return "";
        }
    }

    private static byte[] buildMultipartBody(final String boundary, final byte[] wavBytes) throws IOException {
        final ByteArrayOutputStream out = new ByteArrayOutputStream(wavBytes.length + 2048);
        writeField(out, boundary, "model", MODEL);
        writeField(out, boundary, "response_format", "json");
        writeField(out, boundary, "language", "en");
        writeField(out, boundary, "temperature", "0");
        writeField(out, boundary, "prompt", PROMPT);
        writeFile(out, boundary, "file", "rudolf_voice.wav", "audio/wav", wavBytes);
        writeAscii(out, "--" + boundary + "--\r\n");
        return out.toByteArray();
    }

    private static void writeField(final ByteArrayOutputStream out, final String boundary, final String name, final String value) throws IOException {
        writeAscii(out, "--" + boundary + "\r\n");
        writeAscii(out, "Content-Disposition: form-data; name=\"" + name + "\"\r\n\r\n");
        writeAscii(out, value == null ? "" : value);
        writeAscii(out, "\r\n");
    }

    private static void writeFile(
            final ByteArrayOutputStream out,
            final String boundary,
            final String name,
            final String filename,
            final String contentType,
            final byte[] data
    ) throws IOException {
        writeAscii(out, "--" + boundary + "\r\n");
        writeAscii(out, "Content-Disposition: form-data; name=\"" + name + "\"; filename=\"" + filename + "\"\r\n");
        writeAscii(out, "Content-Type: " + contentType + "\r\n\r\n");
        out.write(data);
        writeAscii(out, "\r\n");
    }

    private static void writeAscii(final ByteArrayOutputStream out, final String value) throws IOException {
        out.write(value.getBytes(StandardCharsets.UTF_8));
    }

    private static byte[] buildWavBytes(final byte[] pcm16, final int sampleRate) {
        final int dataLength = pcm16.length - (pcm16.length % 2);
        final byte[] wav = new byte[44 + dataLength];
        writeAscii(wav, 0, "RIFF");
        writeInt32LittleEndian(wav, 4, 36 + dataLength);
        writeAscii(wav, 8, "WAVE");
        writeAscii(wav, 12, "fmt ");
        writeInt32LittleEndian(wav, 16, 16);
        writeInt16LittleEndian(wav, 20, (short) 1);
        writeInt16LittleEndian(wav, 22, (short) 1);
        writeInt32LittleEndian(wav, 24, sampleRate);
        writeInt32LittleEndian(wav, 28, sampleRate * 2);
        writeInt16LittleEndian(wav, 32, (short) 2);
        writeInt16LittleEndian(wav, 34, (short) 16);
        writeAscii(wav, 36, "data");
        writeInt32LittleEndian(wav, 40, dataLength);
        System.arraycopy(pcm16, 0, wav, 44, dataLength);
        return wav;
    }

    private static void writeAscii(final byte[] target, final int offset, final String value) {
        for (int i = 0; i < value.length(); i++) {
            target[offset + i] = (byte) value.charAt(i);
        }
    }

    private static void writeInt16LittleEndian(final byte[] target, final int offset, final short value) {
        target[offset] = (byte) (value & 0xff);
        target[offset + 1] = (byte) ((value >> 8) & 0xff);
    }

    private static void writeInt32LittleEndian(final byte[] target, final int offset, final int value) {
        target[offset] = (byte) (value & 0xff);
        target[offset + 1] = (byte) ((value >> 8) & 0xff);
        target[offset + 2] = (byte) ((value >> 16) & 0xff);
        target[offset + 3] = (byte) ((value >> 24) & 0xff);
    }
}
