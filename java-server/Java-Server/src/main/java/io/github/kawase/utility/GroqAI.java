package io.github.kawase.utility;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.IOException;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.net.http.HttpTimeoutException;
import java.time.Duration;

public class GroqAI {
    private static final long CACHE_TTL_MS = 5 * 60 * 1000;
    private static final int CACHE_MAX = 200;
    private static final String GROQ_MODEL = "llama-3.3-70b-versatile";
    private static final HttpClient HTTP_CLIENT = HttpClient.newBuilder()
            .connectTimeout(Duration.ofSeconds(8))
            .build();

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

    public static String getConfiguredApiKey() {
        return GroqApiKeys.firstAvailableKeyValue(GroqApiKeys.Purpose.CHAT);
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
        return ask(question, context, "", "en");
    }

    public String ask(final String question, final String context, final String profileContext, final String responseLanguage) {
        StringBuilder promptBuilder = new StringBuilder();
        promptBuilder.append("You are an educational AI mentor inside the Mentora learning game. ");
        promptBuilder.append("Respond in a supportive, concise way that helps the student keep thinking. ");
        promptBuilder.append("Do not give away the full solution unless the student explicitly asks for the final answer.\n");
        if (profileContext != null && !profileContext.isBlank()) {
            promptBuilder.append("Student progress profile:\n").append(profileContext).append("\n");
        }
        promptBuilder.append("Context: ").append(context == null ? "general" : context).append("\n");
        promptBuilder.append("Student request:\n").append(question == null ? "" : question).append("\n");
        promptBuilder.append("Respond entirely in ").append(languageName(responseLanguage))
                .append(" (BCP-47 ").append(normalizeLanguageTag(responseLanguage))
                .append("). Keep programming keywords and code unchanged.\n");
        promptBuilder.append("Keep the answer to 1-4 short sentences.");
        return generate(promptBuilder.toString());
    }

    private String normalizeLanguageTag(final String languageTag) {
        if (languageTag == null) return "en";

        return switch (languageTag) {
            case "en", "ro", "es", "fr", "de", "it", "pt-BR", "pl", "tr", "uk" -> languageTag;
            default -> "en";
        };
    }

    private String languageName(final String languageTag) {
        return switch (normalizeLanguageTag(languageTag)) {
            case "ro" -> "Romanian";
            case "es" -> "Spanish";
            case "fr" -> "French";
            case "de" -> "German";
            case "it" -> "Italian";
            case "pt-BR" -> "Brazilian Portuguese";
            case "pl" -> "Polish";
            case "tr" -> "Turkish";
            case "uk" -> "Ukrainian";
            default -> "English";
        };
    }

    public String generate(final String prompt) {
        String cached = getCached(prompt);
        if (cached != null) {
            System.out.println("[AI] Cache hit.");
            return cached;
        }

        if (!GroqApiKeys.hasConfiguredKeys()) {
            return "AI Error: No Groq API key configured in api-keys.json.";
        }

        String result = callGroq(prompt);
        if (result != null) {
            putCached(prompt, result);
            return result;
        }

        return "AI Error: Groq is temporarily unavailable. Please try again in a moment.";
    }

    private String callGroq(final String prompt) {
        if (!GroqApiKeys.hasConfiguredKeys()) {
            return null;
        }

        String lastRetryableError = "";
        for (int attempt = 0; attempt < GroqApiKeys.configuredKeyCount(); attempt++) {
            GroqApiKeys.ConfiguredKey apiKey = GroqApiKeys.acquireAvailableKey(GroqApiKeys.Purpose.CHAT);
            if (apiKey == null) {
                System.out.println("[AI] All Groq API keys are cooling down.");
                return null;
            }

            String result = callGroqWithKey(prompt, apiKey);
            if (result != null) {
                return result;
            }

            lastRetryableError = "key " + apiKey.label();
        }

        System.out.println("[AI] Groq failed after trying configured API keys. Last retryable source: " + lastRetryableError);
        return null;
    }

    private String callGroqWithKey(final String prompt, final GroqApiKeys.ConfiguredKey apiKey) {
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

            HttpRequest request = HttpRequest.newBuilder()
                    .uri(URI.create("https://api.groq.com/openai/v1/chat/completions"))
                    .timeout(Duration.ofSeconds(35))
                    .header("Content-Type", "application/json")
                    .header("Authorization", "Bearer " + apiKey.value())
                    .POST(HttpRequest.BodyPublishers.ofString(requestBody.toString()))
                    .build();

            HttpResponse<String> response = HTTP_CLIENT.send(request, HttpResponse.BodyHandlers.ofString());

            if (response.statusCode() == 200) {
                JSONObject jsonResponse = new JSONObject(response.body());
                String text = jsonResponse.getJSONArray("choices")
                        .getJSONObject(0)
                        .getJSONObject("message")
                        .getString("content");
                GroqApiKeys.markSuccess(apiKey);
                System.out.println("[AI] Groq success using key " + apiKey.label() + ".");
                return text;
            }

            if (GroqApiKeys.shouldRotateForStatus(response.statusCode())) {
                GroqApiKeys.markFailure(
                        apiKey,
                        GroqApiKeys.cooldownForStatus(response.statusCode(), response.headers()),
                        "HTTP " + response.statusCode()
                );
                return null;
            }

            System.out.println("[AI] Groq error " + response.statusCode() + ": " + response.body());
            return null;
        } catch (HttpTimeoutException e) {
            GroqApiKeys.markFailure(apiKey, GroqApiKeys.defaultCooldownMs(), "request timeout");
            return null;
        } catch (IOException e) {
            GroqApiKeys.markFailure(apiKey, GroqApiKeys.defaultCooldownMs(), e.getClass().getSimpleName());
            return null;
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            GroqApiKeys.markFailure(apiKey, GroqApiKeys.defaultCooldownMs(), "interrupted request");
            return null;
        } catch (Exception e) {
            System.out.println("[AI] Groq exception: " + e.getClass().getName());
            e.printStackTrace();
            return null;
        }
    }
}
