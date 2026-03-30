package io.github.kawase.utility;

import org.json.JSONArray;
import org.json.JSONObject;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;

public class GroqAI {
    private static final Path[] SERVER_KEY_FILES = new Path[] {
            Path.of("api-keys.json"),
            Path.of("java-server", "Java-Server", "api-keys.json")
    };
    private static final String groqApiKey = loadApiKey();
    private static final long CACHE_TTL_MS = 5 * 60 * 1000;
    private static final int CACHE_MAX = 200;
    private static final long COOLDOWN_MS = 60 * 1000;
    private static final String GROQ_MODEL = "llama-3.3-70b-versatile";

    private static volatile long groqCooldownUntilMs = 0L;

    private static final java.util.Map<String, CachedResponse> RESPONSE_CACHE =
            java.util.Collections.synchronizedMap(new java.util.LinkedHashMap<>(CACHE_MAX, 0.75f, true) {
                @Override
                protected boolean removeEldestEntry(final java.util.Map.Entry<String, CachedResponse> eldest) {
                    return size() > CACHE_MAX;
                }
            });

    private static final class CachedResponse {
        private final String text;
        private final long createdAtMs;

        private CachedResponse(final String text, final long createdAtMs) {
            this.text = text;
            this.createdAtMs = createdAtMs;
        }
    }

    private static String loadApiKey() {
        String groq = "";

        try {
            for (Path keyFile : SERVER_KEY_FILES) {
                if (!Files.exists(keyFile)) {
                    continue;
                }

                String content = Files.readString(keyFile, StandardCharsets.UTF_8);
                JSONObject json = new JSONObject(content);
                groq = json.optString("groq_api_key", "");
                System.out.println("[AI] Loaded Groq API key from " + keyFile.toAbsolutePath());
                if (!groq.isBlank()) {
                    break;
                }
            }
        } catch (Exception e) {
            System.out.println("[AI] Could not load Groq API key file: " + e.getClass().getName());
            e.printStackTrace();
        }

        return groq;
    }

    private String getCached(final String prompt) {
        if (prompt == null || prompt.isBlank()) {
            return null;
        }
        CachedResponse cached = RESPONSE_CACHE.get(prompt);
        if (cached == null) {
            return null;
        }
        if (System.currentTimeMillis() - cached.createdAtMs > CACHE_TTL_MS) {
            RESPONSE_CACHE.remove(prompt);
            return null;
        }
        return cached.text;
    }

    private void putCached(final String prompt, final String response) {
        if (prompt == null || prompt.isBlank() || response == null || response.isBlank()) {
            return;
        }
        RESPONSE_CACHE.put(prompt, new CachedResponse(response, System.currentTimeMillis()));
    }

    public String ask(final String question, final String context) {
        return ask(question, context, "");
    }

    public String ask(final String question, final String context, final String profileContext) {
        StringBuilder promptBuilder = new StringBuilder();
        promptBuilder.append("You are an educational AI mentor inside the Mentora learning game. ");
        promptBuilder.append("Respond in a supportive, concise way that helps the student keep thinking. ");
        promptBuilder.append("Do not give away the full solution unless the student explicitly asks for the final answer.\n");
        if (profileContext != null && !profileContext.isBlank()) {
            promptBuilder.append("Student progress profile:\n").append(profileContext).append("\n");
        }
        promptBuilder.append("Context: ").append(context == null ? "general" : context).append("\n");
        promptBuilder.append("Student request:\n").append(question == null ? "" : question).append("\n");
        promptBuilder.append("Keep the answer to 1-4 short sentences.");
        return generate(promptBuilder.toString());
    }

    public String generate(final String prompt) {
        String cached = getCached(prompt);
        if (cached != null) {
            System.out.println("[AI] Cache hit.");
            return cached;
        }

        if (groqApiKey == null || groqApiKey.isBlank()) {
            return "AI Error: No Groq API key configured in api-keys.json.";
        }

        long now = System.currentTimeMillis();
        if (now < groqCooldownUntilMs) {
            return "AI Error: Groq is temporarily rate limited. Please try again in a moment.";
        }

        String result = callGroq(prompt);
        if (result != null) {
            putCached(prompt, result);
            return result;
        }

        return "AI Error: Groq is temporarily unavailable. Please try again in a moment.";
    }

    private String callGroq(final String prompt) {
        try {
            JSONObject requestBody = new JSONObject();
            requestBody.put("model", GROQ_MODEL);
            JSONArray messages = new JSONArray();
            JSONObject msg = new JSONObject();
            msg.put("role", "user");
            msg.put("content", prompt);
            messages.put(msg);
            requestBody.put("messages", messages);
            requestBody.put("max_tokens", 512);

            HttpClient client = HttpClient.newHttpClient();
            HttpRequest request = HttpRequest.newBuilder()
                    .uri(URI.create("https://api.groq.com/openai/v1/chat/completions"))
                    .header("Content-Type", "application/json")
                    .header("Authorization", "Bearer " + groqApiKey)
                    .POST(HttpRequest.BodyPublishers.ofString(requestBody.toString()))
                    .build();

            HttpResponse<String> response = client.send(request, HttpResponse.BodyHandlers.ofString());

            if (response.statusCode() == 200) {
                JSONObject jsonResponse = new JSONObject(response.body());
                String text = jsonResponse.getJSONArray("choices")
                        .getJSONObject(0)
                        .getJSONObject("message")
                        .getString("content");
                System.out.println("[AI] Groq success.");
                return text;
            }

            if (response.statusCode() == 429) {
                groqCooldownUntilMs = System.currentTimeMillis() + COOLDOWN_MS;
                System.out.println("[AI] Groq rate limited (429).");
                return null;
            }

            System.out.println("[AI] Groq error " + response.statusCode() + ": " + response.body());
            return null;
        } catch (Exception e) {
            System.out.println("[AI] Groq exception: " + e.getClass().getName());
            e.printStackTrace();
            return null;
        }
    }
}
