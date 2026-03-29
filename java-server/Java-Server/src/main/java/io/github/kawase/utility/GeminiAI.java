package io.github.kawase.utility;

import java.io.InputStream;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import org.json.JSONObject;
import org.json.JSONArray;

public class GeminiAI {
    private final String apiKey;
    private final String groqApiKey;

    static {
        String[] keys = loadApiKeys();
        _geminiKey = keys[0];
        _groqKey = keys[1];
    }
    private static final String _geminiKey;
    private static final String _groqKey;

    private static String[] loadApiKeys() {
        String gemini = "";
        String groq = "";

        // Try loading from api-keys.json next to the jar / in working directory
        Path externalFile = Path.of("api-keys.json");
        try {
            String content = null;
            if (Files.exists(externalFile)) {
                content = Files.readString(externalFile, StandardCharsets.UTF_8);
                System.out.println("[AI] Loaded API keys from " + externalFile.toAbsolutePath());
            } else {
                // Try classpath resource
                try (InputStream is = GeminiAI.class.getResourceAsStream("/api-keys.json")) {
                    if (is != null) {
                        content = new String(is.readAllBytes(), StandardCharsets.UTF_8);
                        System.out.println("[AI] Loaded API keys from classpath resource.");
                    }
                }
            }
            if (content != null) {
                JSONObject json = new JSONObject(content);
                gemini = json.optString("gemini_api_key", "");
                groq = json.optString("groq_api_key", "");
            }
        } catch (Exception e) {
            System.out.println("[AI] Could not load api-keys.json: " + e.getMessage());
        }

        // Env vars override file values
        String envGemini = System.getenv("GEMINI_API_KEY");
        String envGroq = System.getenv("GROQ_API_KEY");
        if (envGemini != null && !envGemini.isBlank()) gemini = envGemini;
        if (envGroq != null && !envGroq.isBlank()) groq = envGroq;

        return new String[]{gemini, groq};
    }

    public GeminiAI() {
        this.apiKey = _geminiKey;
        this.groqApiKey = _groqKey;
    }
    private static final long CACHE_TTL_MS = 5 * 60 * 1000;
    private static final int CACHE_MAX = 200;
    private static final long COOLDOWN_MS = 60 * 1000;

    private static final String[] GEMINI_MODELS = {
            "gemini-2.5-flash",
            "gemini-2.0-flash"
    };
    private static final String GROQ_MODEL = "llama-3.3-70b-versatile";

    private static volatile long[] modelCooldownUntilMs = new long[GEMINI_MODELS.length];
    private static volatile long groqCooldownUntilMs = 0L;

    private static final java.util.Map<String, CachedResponse> RESPONSE_CACHE =
            java.util.Collections.synchronizedMap(new java.util.LinkedHashMap<>(CACHE_MAX, 0.75f, true) {
                @Override
                protected boolean removeEldestEntry(java.util.Map.Entry<String, CachedResponse> eldest) {
                    return size() > CACHE_MAX;
                }
            });

    private static final class CachedResponse {
        private final String text;
        private final long createdAtMs;

        private CachedResponse(String text, long createdAtMs) {
            this.text = text;
            this.createdAtMs = createdAtMs;
        }
    }

    private String getCached(String prompt) {
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

    private void putCached(String prompt, String response) {
        if (prompt == null || prompt.isBlank() || response == null || response.isBlank()) {
            return;
        }
        RESPONSE_CACHE.put(prompt, new CachedResponse(response, System.currentTimeMillis()));
    }

    public String ask(String question, String context) {
        return ask(question, context, "");
    }

    public String ask(String question, String context, String profileSummary) {
        StringBuilder promptBuilder = new StringBuilder();
        promptBuilder.append("You are an educational AI mentor for a student learning C++ in a game called Mentora. ");
        if (profileSummary != null && !profileSummary.isBlank()) {
            promptBuilder.append("Student profile: ").append(profileSummary).append("\n");
        }
        promptBuilder.append("Context: ").append(context).append("\n")
                .append("Student's Question: ").append(question).append("\n")
                .append("Please provide a helpful, encouraging, and easy-to-understand response for a student. ")
                .append("Keep it concise (1-3 sentences).");

        return callWithFallback(promptBuilder.toString());
    }

    public String generate(String prompt) {
        return callWithFallback(prompt);
    }

    private String callWithFallback(String prompt) {
        String cached = getCached(prompt);
        if (cached != null) {
            System.out.println("[AI] Cache hit.");
            return cached;
        }

        // Try each Gemini model in order
        if (apiKey != null && !apiKey.isBlank()) {
            long now = System.currentTimeMillis();
            for (int i = 0; i < GEMINI_MODELS.length; i++) {
                if (now < modelCooldownUntilMs[i]) {
                    System.out.println("[AI] " + GEMINI_MODELS[i] + " on cooldown, skipping.");
                    continue;
                }

                String result = callGemini(prompt, GEMINI_MODELS[i], i);
                if (result != null) {
                    putCached(prompt, result);
                    return result;
                }
            }
        }

        // Fallback to Groq
        if (groqApiKey != null && !groqApiKey.isBlank()) {
            long now = System.currentTimeMillis();
            if (now >= groqCooldownUntilMs) {
                String result = callGroq(prompt);
                if (result != null) {
                    putCached(prompt, result);
                    return result;
                }
            } else {
                System.out.println("[AI] Groq on cooldown, skipping.");
            }
        }

        if ((apiKey == null || apiKey.isBlank()) && (groqApiKey == null || groqApiKey.isBlank())) {
            return "AI Error: No API keys configured. Set GEMINI_API_KEY or GROQ_API_KEY.";
        }

        return "AI Error: All models are rate limited. Please try again in a moment.";
    }

    private String callGemini(String prompt, String model, int modelIndex) {
        try {
            JSONObject requestBody = new JSONObject();
            JSONArray contents = new JSONArray();
            JSONObject part = new JSONObject();
            part.put("text", prompt);
            JSONArray parts = new JSONArray();
            parts.put(part);
            JSONObject content = new JSONObject();
            content.put("parts", parts);
            contents.put(content);
            requestBody.put("contents", contents);

            HttpClient client = HttpClient.newHttpClient();

            HttpRequest request = HttpRequest.newBuilder()
                    .uri(URI.create("https://generativelanguage.googleapis.com/v1beta/models/" + model + ":generateContent?key=" + apiKey))
                    .header("Content-Type", "application/json")
                    .POST(HttpRequest.BodyPublishers.ofString(requestBody.toString()))
                    .build();

            HttpResponse<String> response = client.send(request, HttpResponse.BodyHandlers.ofString());

            if (response.statusCode() == 200) {
                JSONObject jsonResponse = new JSONObject(response.body());
                String text = jsonResponse.getJSONArray("candidates")
                        .getJSONObject(0)
                        .getJSONObject("content")
                        .getJSONArray("parts")
                        .getJSONObject(0)
                        .getString("text");
                System.out.println("[AI] " + model + " success.");
                return text;
            } else if (response.statusCode() == 429) {
                modelCooldownUntilMs[modelIndex] = System.currentTimeMillis() + COOLDOWN_MS;
                System.out.println("[AI] " + model + " rate limited (429), trying next model...");
                return null;
            } else {
                System.out.println("[AI] " + model + " error " + response.statusCode() + ": " + response.body());
                return null;
            }
        } catch (Exception e) {
            System.out.println("[AI] " + model + " exception: " + e.getMessage());
            return null;
        }
    }

    private String callGroq(String prompt) {
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
            } else if (response.statusCode() == 429) {
                groqCooldownUntilMs = System.currentTimeMillis() + COOLDOWN_MS;
                System.out.println("[AI] Groq rate limited (429).");
                return null;
            } else {
                System.out.println("[AI] Groq error " + response.statusCode() + ": " + response.body());
                return null;
            }
        } catch (Exception e) {
            System.out.println("[AI] Groq exception: " + e.getMessage());
            return null;
        }
    }
}
