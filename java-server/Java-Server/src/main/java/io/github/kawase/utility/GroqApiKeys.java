package io.github.kawase.utility;

import org.json.JSONArray;
import org.json.JSONObject;

import java.net.http.HttpHeaders;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.ArrayList;
import java.util.Collections;
import java.util.EnumMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Optional;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;

final class GroqApiKeys {
    private static final Path[] SERVER_KEY_FILES = new Path[] {
            Path.of("api-keys.json"),
            Path.of("java-server", "Java-Server", "api-keys.json")
    };
    private static final long DEFAULT_COOLDOWN_MS = Duration.ofSeconds(60).toMillis();
    private static final long INVALID_KEY_COOLDOWN_MS = Duration.ofMinutes(10).toMillis();
    private static final long MAX_RETRY_AFTER_MS = Duration.ofMinutes(10).toMillis();
    private static final List<String> API_KEYS = loadApiKeys();
    private static final EnumMap<Purpose, KeyState> KEY_STATES = buildKeyStates();

    private GroqApiKeys() {
    }

    enum Purpose {
        CHAT("chat"),
        STT("stt");

        private final String label;

        Purpose(final String label) {
            this.label = label;
        }
    }

    private static final class KeyState {
        private final AtomicInteger currentIndex = new AtomicInteger(0);
        private final ConcurrentHashMap<Integer, Long> cooldownUntilByIndex = new ConcurrentHashMap<>();
    }

    record ConfiguredKey(Purpose purpose, int index, String value) {
        String label() {
            return purpose.label + " #" + (index + 1) + "/" + API_KEYS.size();
        }
    }

    static boolean hasConfiguredKeys() {
        return !API_KEYS.isEmpty();
    }

    static int configuredKeyCount() {
        return API_KEYS.size();
    }

    static String firstAvailableKeyValue(final Purpose purpose) {
        ConfiguredKey key = acquireAvailableKey(purpose);
        return key == null ? "" : key.value();
    }

    static ConfiguredKey acquireAvailableKey(final Purpose purpose) {
        if (API_KEYS.isEmpty()) {
            return null;
        }

        Purpose resolvedPurpose = purpose == null ? Purpose.CHAT : purpose;
        KeyState state = stateFor(resolvedPurpose);
        long now = System.currentTimeMillis();
        int start = Math.floorMod(state.currentIndex.get(), API_KEYS.size());
        for (int offset = 0; offset < API_KEYS.size(); offset++) {
            int index = (start + offset) % API_KEYS.size();
            long cooldownUntil = state.cooldownUntilByIndex.getOrDefault(index, 0L);
            if (cooldownUntil <= now) {
                state.currentIndex.set(index);
                return new ConfiguredKey(resolvedPurpose, index, API_KEYS.get(index));
            }
        }

        return null;
    }

    static void markSuccess(final ConfiguredKey key) {
        if (key == null) {
            return;
        }
        KeyState state = stateFor(key.purpose());
        state.cooldownUntilByIndex.remove(key.index());
        state.currentIndex.set(key.index());
    }

    static void markFailure(final ConfiguredKey key, final long cooldownMs, final String reason) {
        if (key == null || API_KEYS.isEmpty()) {
            return;
        }

        long resolvedCooldownMs = Math.max(1_000L, cooldownMs);
        KeyState state = stateFor(key.purpose());
        state.cooldownUntilByIndex.put(key.index(), System.currentTimeMillis() + resolvedCooldownMs);
        state.currentIndex.set((key.index() + 1) % API_KEYS.size());
        System.out.println("[GroqKeys] Key " + key.label() + " cooling down for "
                + (resolvedCooldownMs / 1000L) + "s after " + reason + ".");
    }

    static boolean shouldRotateForStatus(final int statusCode) {
        return statusCode == 401
                || statusCode == 403
                || statusCode == 408
                || statusCode == 429
                || statusCode == 500
                || statusCode == 502
                || statusCode == 503
                || statusCode == 504;
    }

    static long cooldownForStatus(final int statusCode, final HttpHeaders headers) {
        if (statusCode == 401 || statusCode == 403) {
            return INVALID_KEY_COOLDOWN_MS;
        }
        if (statusCode == 429) {
            Optional<String> retryAfter = headers == null ? Optional.empty() : headers.firstValue("Retry-After");
            long parsedMs = parseRetryAfterMs(retryAfter.orElse(""));
            if (parsedMs > 0L) {
                return Math.min(parsedMs, MAX_RETRY_AFTER_MS);
            }
        }
        return DEFAULT_COOLDOWN_MS;
    }

    static long defaultCooldownMs() {
        return DEFAULT_COOLDOWN_MS;
    }

    private static EnumMap<Purpose, KeyState> buildKeyStates() {
        EnumMap<Purpose, KeyState> states = new EnumMap<>(Purpose.class);
        for (Purpose purpose : Purpose.values()) {
            states.put(purpose, new KeyState());
        }
        return states;
    }

    private static KeyState stateFor(final Purpose purpose) {
        return KEY_STATES.get(purpose == null ? Purpose.CHAT : purpose);
    }

    private static List<String> loadApiKeys() {
        Set<String> keys = new LinkedHashSet<>();

        for (Path keyFile : SERVER_KEY_FILES) {
            if (!Files.exists(keyFile)) {
                continue;
            }

            try {
                String content = Files.readString(keyFile, StandardCharsets.UTF_8);
                JSONObject json = new JSONObject(content);
                addJsonKeys(keys, json, "groq_api_keys");
                addJsonKeys(keys, json, "groqApiKeys");
                addJsonKeys(keys, json, "grok_api_keys");
                addJsonKeys(keys, json, "groq_api_key");
                addJsonKeys(keys, json, "groqApiKey");
                addJsonKeys(keys, json, "grok_api_key");
                if (!keys.isEmpty()) {
                    System.out.println("[GroqKeys] Loaded " + keys.size() + " Groq API key(s) from "
                            + keyFile.toAbsolutePath());
                    break;
                }
            } catch (Exception e) {
                System.out.println("[GroqKeys] Could not load Groq API key file: " + e.getClass().getName());
                e.printStackTrace();
            }
        }

        if (keys.isEmpty()) {
            addDelimitedKeys(keys, System.getenv("GROQ_API_KEYS"));
            addDelimitedKeys(keys, System.getenv("GROQ_API_KEY"));
            if (!keys.isEmpty()) {
                System.out.println("[GroqKeys] Loaded " + keys.size() + " Groq API key(s) from environment.");
            }
        }

        return Collections.unmodifiableList(new ArrayList<>(keys));
    }

    private static void addJsonKeys(final Set<String> keys, final JSONObject json, final String fieldName) {
        if (!json.has(fieldName)) {
            return;
        }

        Object value = json.opt(fieldName);
        if (value instanceof JSONArray array) {
            for (int i = 0; i < array.length(); i++) {
                addDelimitedKeys(keys, array.optString(i, ""));
            }
            return;
        }

        addDelimitedKeys(keys, json.optString(fieldName, ""));
    }

    private static void addDelimitedKeys(final Set<String> keys, final String rawValue) {
        if (rawValue == null || rawValue.isBlank()) {
            return;
        }

        String[] parts = rawValue.split("[,\\n\\r;]+");
        for (String part : parts) {
            String key = part == null ? "" : part.trim();
            if (!key.isBlank()) {
                keys.add(key);
            }
        }
    }

    private static long parseRetryAfterMs(final String rawRetryAfter) {
        if (rawRetryAfter == null || rawRetryAfter.isBlank()) {
            return 0L;
        }

        String trimmed = rawRetryAfter.trim().toLowerCase(Locale.ROOT);
        try {
            return Math.max(0L, Long.parseLong(trimmed)) * 1000L;
        } catch (NumberFormatException ignored) {
            return 0L;
        }
    }
}
