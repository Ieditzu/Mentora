package io.github.kawase.database.services;

import io.github.kawase.cpp.CppExecutor;
import io.github.kawase.database.entity.Child;
import io.github.kawase.database.repository.ChildRepository;
import io.github.kawase.database.repository.CompletedTaskRepository;
import io.github.kawase.python.PythonExecutor;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneId;
import java.time.format.DateTimeFormatter;
import java.time.temporal.TemporalAdjusters;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

@Service
@RequiredArgsConstructor
public class LearningProfileService {

    private final ChildRepository childRepository;
    private final CompletedTaskRepository completedTaskRepository;
    private static final long SUMMARY_MIN_INTERVAL_SECONDS = 300;

    @Transactional
    public void recordLearningEvent(final Long childId, final String eventType, final String topic, final int correctness, final String details) {
        if (childId == null) {
            return;
        }

        Child child = childRepository.findById(childId)
                .orElseThrow(() -> new RuntimeException("Child not found"));

        Map<String, Object> gameStats = child.getGameStats();
        if (gameStats == null) {
            gameStats = new HashMap<>();
        }

        String language = deriveLanguage(topic, details);
        String resolvedTopic = (topic == null || topic.isBlank()) ? "general" : topic.trim();

        if (language == null) {
            updateProfile(gameStats, "aiProfileGeneral", eventType, resolvedTopic, correctness, details);
        } else {
            updateProfile(gameStats, language.equals("cpp") ? "aiProfileCpp" : "aiProfilePython", eventType, resolvedTopic, correctness, details);
            updateProfile(gameStats, "aiProfileGeneral", eventType, resolvedTopic, correctness, details);
        }

        child.setGameStats(gameStats);
        childRepository.save(child);
    }

    @Transactional
    public void recordAiInteraction(final Long childId, final String context, final String question) {
        if (childId == null) {
            return;
        }

        if (context != null && context.toLowerCase().contains("eval")) {
            return;
        }

        String eventType = (context != null && context.toLowerCase().contains("hint")) ? "ai_hint" : "ai_chat";
        String topic = deriveTopic(context, question);
        recordLearningEvent(childId, eventType, topic, -1, question);
    }

    @Transactional(readOnly = true)
    public String buildProfileSummary(final Long childId) {
        return buildProfileSummary(childId, null);
    }

    @Transactional(readOnly = true)
    public String buildProfileSummary(final Long childId, final String language) {
        if (childId == null) {
            return "";
        }

        Child child = childRepository.findById(childId).orElse(null);
        if (child == null) {
            return "";
        }

        Map<String, Object> gameStats = child.getGameStats();
        if (gameStats == null) {
            return "No prior learning profile yet.";
        }

        String profileKey = null;
        if (language != null) {
            profileKey = language.equals("cpp") ? "aiProfileCpp" : language.equals("python") ? "aiProfilePython" : null;
        }

        Map<String, Object> aiProfile = profileKey != null ? safeMap(gameStats.get(profileKey)) : Collections.emptyMap();
        Map<String, Object> generalProfile = safeMap(gameStats.get("aiProfileGeneral"));

        int correct = getInt(aiProfile.get("correctCount"));
        int incorrect = getInt(aiProfile.get("incorrectCount"));
        int total = correct + incorrect;
        double accuracy = total == 0 ? 0.0 : (double) correct / Math.max(1, total);

        String level;
        if (total < 4) {
            level = "beginner";
        } else if (accuracy >= 0.85 && total >= 8) {
            level = "advanced";
        } else if (accuracy >= 0.65) {
            level = "intermediate";
        } else {
            level = "beginner";
        }

        List<String> strengths = new ArrayList<>();
        List<String> needsHelp = new ArrayList<>();
        Map<String, Object> topics = safeMap(aiProfile.get("topics"));
        List<Map.Entry<String, Integer>> scored = new ArrayList<>();
        for (Map.Entry<String, Object> entry : topics.entrySet()) {
            Map<String, Object> topicStats = safeMap(entry.getValue());
            int topicCorrect = getInt(topicStats.get("correct"));
            int topicIncorrect = getInt(topicStats.get("incorrect"));
            int score = topicCorrect - topicIncorrect;
            scored.add(Map.entry(entry.getKey(), score));
        }

        scored.sort((a, b) -> Integer.compare(b.getValue(), a.getValue()));
        for (Map.Entry<String, Integer> entry : scored) {
            if (entry.getValue() <= 0) {
                continue;
            }
            strengths.add(entry.getKey());
            if (strengths.size() >= 3) {
                break;
            }
        }

        scored.sort((a, b) -> Integer.compare(a.getValue(), b.getValue()));
        for (Map.Entry<String, Integer> entry : scored) {
            if (entry.getValue() >= 0) {
                continue;
            }
            needsHelp.add(entry.getKey());
            if (needsHelp.size() >= 3) {
                break;
            }
        }

        StringBuilder summary = new StringBuilder();
        if (language != null) {
            summary.append("Language: ").append(language).append(". ");
        }
        summary.append("Student level: ").append(level).append(". ");
        if (!strengths.isEmpty()) {
            summary.append("Strengths: ").append(String.join(", ", strengths)).append(". ");
        }
        if (!needsHelp.isEmpty()) {
            summary.append("Needs help with: ").append(String.join(", ", needsHelp)).append(". ");
        }
        summary.append("Accuracy: ").append(correct).append(" correct out of ").append(total).append(". ");
        List<String> struggleConcepts = topConcepts(aiProfile, true);
        if (!struggleConcepts.isEmpty()) {
            summary.append("Struggles: ").append(String.join(", ", struggleConcepts)).append(". ");
        }
        List<String> commonMistakes = topMistakes(aiProfile);
        if (!commonMistakes.isEmpty()) {
            summary.append("Common mistakes: ").append(String.join(", ", commonMistakes)).append(". ");
        }
        int genCorrect = getInt(generalProfile.get("correctCount"));
        int genIncorrect = getInt(generalProfile.get("incorrectCount"));
        int genTotal = genCorrect + genIncorrect;
        if (genTotal > 0) {
            summary.append("Overall programming accuracy: ").append(genCorrect).append(" correct out of ").append(genTotal).append(".");
        }
        return summary.toString();
    }

    @Transactional(readOnly = true)
    public String buildAiHelpProfileContext(final Long childId, final String language) {
        if (childId == null) {
            return "";
        }

        Child child = childRepository.findById(childId).orElse(null);
        if (child == null) {
            return "";
        }

        Map<String, Object> gameStats = child.getGameStats();
        if (gameStats == null) {
            return "No tracked progress yet.";
        }

        String profileKey = null;
        if (language != null) {
            profileKey = language.equals("cpp") ? "aiProfileCpp" : language.equals("python") ? "aiProfilePython" : null;
        }

        Map<String, Object> aiProfile = profileKey != null ? safeMap(gameStats.get(profileKey)) : Collections.emptyMap();
        Map<String, Object> generalProfile = safeMap(gameStats.get("aiProfileGeneral"));

        int specificCorrect = getInt(aiProfile.get("correctCount"));
        int specificIncorrect = getInt(aiProfile.get("incorrectCount"));
        int specificTotal = specificCorrect + specificIncorrect;
        int generalCorrect = getInt(generalProfile.get("correctCount"));
        int generalIncorrect = getInt(generalProfile.get("incorrectCount"));
        int generalTotal = generalCorrect + generalIncorrect;

        StringBuilder context = new StringBuilder();
        context.append("Child: ").append(child.getName() == null || child.getName().isBlank() ? "unknown" : child.getName()).append('\n');
        context.append("Current track: ").append(language == null ? "general" : language).append('\n');
        context.append("Track summary: ").append(buildProfileSummary(childId, language)).append('\n');
        context.append("Track stats: ")
                .append("correct=").append(specificCorrect)
                .append(", incorrect=").append(specificIncorrect)
                .append(", total=").append(specificTotal)
                .append(", hintsUsed=").append(getInt(aiProfile.get("hintsUsed")))
                .append(", chatTurns=").append(getInt(aiProfile.get("chatTurns")))
                .append('\n');
        context.append("Overall stats: ")
                .append("correct=").append(generalCorrect)
                .append(", incorrect=").append(generalIncorrect)
                .append(", total=").append(generalTotal)
                .append(", totalInteractions=").append(getInt(generalProfile.get("totalInteractions")))
                .append('\n');
        appendListLine(context, "Strengths", topConcepts(aiProfile, false));
        appendListLine(context, "Struggles", topConcepts(aiProfile, true));
        appendListLine(context, "Common mistakes", topMistakes(aiProfile));
        appendListLine(context, "Frequent help topics", topHelpTopics(aiProfile));
        appendRecentEventsLine(context, aiProfile);
        return context.toString().trim();
    }

    @Transactional(readOnly = true)
    public String buildMultiplayerProgrammingProfileSummary(final Long childId) {
        if (childId == null) {
            return "";
        }

        String context = buildAiHelpProfileContext(childId, null);
        if (context == null || context.isBlank()) {
            return "No tracked programming profile yet.";
        }

        return context
                .replace('\r', ' ')
                .replace('\n', ' ')
                .replaceAll("\\s{2,}", " ")
                .trim();
    }

    @Transactional(readOnly = true)
    public String generateWeeklyParentReport(final Long childId) {
        if (childId == null) {
            return "";
        }

        Child child = childRepository.findById(childId).orElse(null);
        if (child == null) {
            return "";
        }

        LocalDate today = LocalDate.now();
        LocalDate weekStart = today.with(TemporalAdjusters.previousOrSame(java.time.DayOfWeek.MONDAY));
        LocalDate weekEnd = weekStart.plusDays(6);
        String childName = child.getName() == null || child.getName().isBlank() ? "your child" : child.getName();

        var completedThisWeek = completedTaskRepository.findByChildIdOrderByCompletedAtDesc(childId).stream()
                .filter(ct -> ct.getCompletedAt() != null)
                .filter(ct -> {
                    LocalDate completedDate = ct.getCompletedAt().withZoneSameInstant(ZoneId.systemDefault()).toLocalDate();
                    return !completedDate.isBefore(weekStart) && !completedDate.isAfter(weekEnd);
                })
                .toList();

        Map<String, Object> stats = child.getGameStats() == null ? Collections.emptyMap() : child.getGameStats();
        Map<String, Object> cpp = safeMap(stats.get("aiProfileCpp"));
        Map<String, Object> python = safeMap(stats.get("aiProfilePython"));
        Map<String, Object> general = safeMap(stats.get("aiProfileGeneral"));

        List<String> recentEvents = collectRecentWeeklyEvents(general, weekStart, weekEnd);
        if (recentEvents.isEmpty()) {
            recentEvents.addAll(collectRecentWeeklyEvents(cpp, weekStart, weekEnd));
            recentEvents.addAll(collectRecentWeeklyEvents(python, weekStart, weekEnd));
        }

        String prompt = buildWeeklyReportPrompt(childName, weekStart, weekEnd, completedThisWeek, cpp, python, general, recentEvents);
        io.github.kawase.utility.GroqAI ai = new io.github.kawase.utility.GroqAI();
        String report = ai.generate(prompt);
        if (report == null || report.isBlank() || report.startsWith("AI Error")) {
            return buildFallbackWeeklyReport(childName, weekStart, weekEnd, completedThisWeek, cpp, python, general);
        }

        return report.trim();
    }

    private String buildWeeklyReportPrompt(final String childName,
                                           final LocalDate weekStart,
                                           final LocalDate weekEnd,
                                           final List<io.github.kawase.database.entity.CompletedTask> completedTasks,
                                           final Map<String, Object> cpp,
                                           final Map<String, Object> python,
                                           final Map<String, Object> general,
                                           final List<String> recentEvents) {
        int cppCorrect = getInt(cpp.get("correctCount"));
        int cppIncorrect = getInt(cpp.get("incorrectCount"));
        int pyCorrect = getInt(python.get("correctCount"));
        int pyIncorrect = getInt(python.get("incorrectCount"));
        int generalCorrect = getInt(general.get("correctCount"));
        int generalIncorrect = getInt(general.get("incorrectCount"));

        Map<String, Integer> taskCounts = new LinkedHashMap<>();
        for (var completedTask : completedTasks) {
            String title = completedTask.getTask() == null ? "Task" : completedTask.getTask().getTitle();
            taskCounts.put(title, taskCounts.getOrDefault(title, 0) + 1);
        }

        return "You are the Mentora AI tutor writing a weekly report card letter to a parent.\n" +
                "Write in warm natural language, not a table. Keep it to 2 short paragraphs plus one actionable next step.\n" +
                "Do not invent facts beyond the data. If data is sparse, say that plainly.\n\n" +
                "Child: " + childName + "\n" +
                "Week: " + weekStart + " through " + weekEnd + "\n" +
                "Completed tasks this week: " + completedTasks.size() + "\n" +
                "Task titles/counts: " + (taskCounts.isEmpty() ? "none" : taskCounts.toString()) + "\n" +
                "C++ attempts: correct=" + cppCorrect + ", incorrect=" + cppIncorrect + ", hints=" + getInt(cpp.get("hintsUsed")) + ", common mistakes=" + topMistakes(cpp) + ", struggles=" + topConcepts(cpp, true) + "\n" +
                "Python attempts: correct=" + pyCorrect + ", incorrect=" + pyIncorrect + ", hints=" + getInt(python.get("hintsUsed")) + ", common mistakes=" + topMistakes(python) + ", struggles=" + topConcepts(python, true) + "\n" +
                "Overall attempts: correct=" + generalCorrect + ", incorrect=" + generalIncorrect + ", hints=" + getInt(general.get("hintsUsed")) + ", chatTurns=" + getInt(general.get("chatTurns")) + "\n" +
                "Recent tracked events from this week: " + (recentEvents.isEmpty() ? "none" : String.join(" | ", recentEvents)) + "\n\n" +
                "Write the letter starting with: This week " + childName + " ";
    }

    private String buildFallbackWeeklyReport(final String childName,
                                             final LocalDate weekStart,
                                             final LocalDate weekEnd,
                                             final List<io.github.kawase.database.entity.CompletedTask> completedTasks,
                                             final Map<String, Object> cpp,
                                             final Map<String, Object> python,
                                             final Map<String, Object> general) {
        int correct = getInt(general.get("correctCount"));
        int incorrect = getInt(general.get("incorrectCount"));
        int total = correct + incorrect;
        List<String> struggles = topConcepts(general, true);
        List<String> mistakes = topMistakes(general);

        StringBuilder builder = new StringBuilder();
        builder.append("This week ").append(childName).append(" completed ")
                .append(completedTasks.size()).append(" task")
                .append(completedTasks.size() == 1 ? "" : "s")
                .append(" between ").append(weekStart).append(" and ").append(weekEnd).append(". ");
        if (total > 0) {
            builder.append("Across tracked attempts, the profile shows ")
                    .append(correct).append(" correct and ").append(incorrect).append(" incorrect responses. ");
        } else {
            builder.append("There is not enough attempt-level data yet for a precise pattern analysis. ");
        }
        if (!struggles.isEmpty()) {
            builder.append("The main areas to revisit are ").append(String.join(", ", struggles)).append(". ");
        }
        if (!mistakes.isEmpty()) {
            builder.append("The most common mistake pattern is ").append(String.join(", ", mistakes)).append(". ");
        }
        builder.append("For the next session, choose one short challenge in the weakest area and ask for a hint only after the first independent attempt.");
        return builder.toString();
    }

    private List<String> collectRecentWeeklyEvents(final Map<String, Object> profile, final LocalDate weekStart, final LocalDate weekEnd) {
        Object recentValue = profile.get("recentEvents");
        if (!(recentValue instanceof List<?> recentEvents) || recentEvents.isEmpty()) {
            return new ArrayList<>();
        }

        List<String> events = new ArrayList<>();
        for (Object item : recentEvents) {
            if (!(item instanceof Map<?, ?> rawMap)) {
                continue;
            }

            String ts = rawMap.get("ts") == null ? "" : rawMap.get("ts").toString();
            try {
                LocalDate eventDate = Instant.parse(ts).atZone(ZoneId.systemDefault()).toLocalDate();
                if (eventDate.isBefore(weekStart) || eventDate.isAfter(weekEnd)) {
                    continue;
                }
            } catch (Exception ignored) {
                continue;
            }

            String type = rawMap.get("type") == null ? "event" : rawMap.get("type").toString();
            String topic = rawMap.get("topic") == null ? "general" : rawMap.get("topic").toString();
            String correctness = rawMap.get("correctness") == null ? "unknown" : rawMap.get("correctness").toString();
            String detail = rawMap.get("detail") == null ? "" : truncate(rawMap.get("detail").toString(), 90);
            events.add(type + " on " + topic + " (" + correctness + ")" + (detail.isBlank() ? "" : ": " + detail));
        }

        return events;
    }

    @Transactional
    public void ensureAiSummaries(final Long childId) {
        if (childId == null) {
            return;
        }
        Child child = childRepository.findById(childId).orElse(null);
        if (child == null) {
            return;
        }
        Map<String, Object> gameStats = child.getGameStats();
        if (gameStats == null) {
            return;
        }

        ensureAiSummaryForProfile(gameStats, "aiProfileCpp", "C++");
        ensureAiSummaryForProfile(gameStats, "aiProfilePython", "Python");
        ensureAiSummaryForProfile(gameStats, "aiProfileGeneral", "General");

        child.setGameStats(gameStats);
        childRepository.save(child);
    }

    private List<String> topConcepts(final Map<String, Object> profile, final boolean struggles) {
        Map<String, Object> concepts = safeMap(profile.get("concepts"));
        List<Map.Entry<String, Integer>> scored = new ArrayList<>();
        for (Map.Entry<String, Object> entry : concepts.entrySet()) {
            Map<String, Object> stats = safeMap(entry.getValue());
            int correct = getInt(stats.get("correct"));
            int incorrect = getInt(stats.get("incorrect"));
            int score = struggles ? (incorrect - correct) : (correct - incorrect);
            scored.add(Map.entry(entry.getKey(), score));
        }
        scored.sort((a, b) -> Integer.compare(b.getValue(), a.getValue()));
        List<String> results = new ArrayList<>();
        for (Map.Entry<String, Integer> entry : scored) {
            if (entry.getValue() <= 0) {
                continue;
            }
            results.add(entry.getKey());
            if (results.size() >= 3) break;
        }
        return results;
    }

    private List<String> topMistakes(final Map<String, Object> profile) {
        Map<String, Object> mistakes = safeMap(profile.get("mistakes"));
        List<Map.Entry<String, Integer>> scored = new ArrayList<>();
        for (Map.Entry<String, Object> entry : mistakes.entrySet()) {
            scored.add(Map.entry(entry.getKey(), getInt(entry.getValue())));
        }
        scored.sort((a, b) -> Integer.compare(b.getValue(), a.getValue()));
        List<String> results = new ArrayList<>();
        for (Map.Entry<String, Integer> entry : scored) {
            if (entry.getValue() <= 0) continue;
            results.add(entry.getKey());
            if (results.size() >= 3) break;
        }
        return results;
    }

    private void ensureAiSummaryForProfile(final Map<String, Object> gameStats, final String profileKey, final String label) {
        Map<String, Object> profile = safeMap(gameStats.get(profileKey));
        if (profile.isEmpty()) {
            return;
        }

        String lastUpdated = profile.get("lastUpdated") != null ? profile.get("lastUpdated").toString() : "";
        String summaryUpdated = profile.get("summaryUpdated") != null ? profile.get("summaryUpdated").toString() : "";
        if (!summaryUpdated.isEmpty() && !lastUpdated.isEmpty() && summaryUpdated.compareTo(lastUpdated) >= 0) {
            return;
        }
        if (!summaryUpdated.isEmpty()) {
            try {
                Instant lastSummary = Instant.parse(summaryUpdated);
                if (Instant.now().minusSeconds(SUMMARY_MIN_INTERVAL_SECONDS).isBefore(lastSummary)) {
                    return;
                }
            } catch (Exception ignored) {
                // If parsing fails, continue and regenerate.
            }
        }

        String prompt = buildSummaryPrompt(profile, label);
        io.github.kawase.utility.GroqAI ai = new io.github.kawase.utility.GroqAI();
        String summary = ai.generate(prompt);
        if (summary == null) {
            return;
        }

        String trimmed = summary.trim();
        String oneLine = trimmed;
        String threeLine = trimmed;

        int oneIdx = trimmed.indexOf("ONE:");
        int threeIdx = trimmed.indexOf("THREE:");
        if (oneIdx != -1 && threeIdx != -1) {
            oneLine = trimmed.substring(oneIdx + 4, threeIdx).trim();
            threeLine = trimmed.substring(threeIdx + 6).trim();
        }

        profile.put("summaryText", trimmed);
        profile.put("summaryOneLine", oneLine);
        profile.put("summaryThreeLine", threeLine);
        profile.put("summaryUpdated", Instant.now().toString());
        gameStats.put(profileKey, profile);
    }

    private String buildSummaryPrompt(final Map<String, Object> profile, final String label) {
        int correct = getInt(profile.get("correctCount"));
        int incorrect = getInt(profile.get("incorrectCount"));
        int total = correct + incorrect;
        int hintsUsed = getInt(profile.get("hintsUsed"));
        int chatTurns = getInt(profile.get("chatTurns"));
        double accuracy = total == 0 ? 0.0 : (double) correct / Math.max(1, total);
        List<String> strengths = topConcepts(profile, false);
        List<String> struggles = topConcepts(profile, true);
        List<String> mistakes = topMistakes(profile);
        List<String> helpTopics = topHelpTopics(profile);

        return "You are summarizing a student's " + label + " learning profile for their parent.\n" +
                "Data:\n" +
                "- Correct: " + correct + ", Incorrect: " + incorrect + ", Accuracy: " + String.format("%.0f%%", accuracy * 100) + "\n" +
                "- Hints used: " + hintsUsed + ", AI chat turns: " + chatTurns + "\n" +
                "- Strengths: " + (strengths.isEmpty() ? "none yet" : String.join(", ", strengths)) + "\n" +
                "- Struggles: " + (struggles.isEmpty() ? "none yet" : String.join(", ", struggles)) + "\n" +
                "- Common mistakes: " + (mistakes.isEmpty() ? "none" : String.join(", ", mistakes)) + "\n" +
                "- Asked AI about: " + (helpTopics.isEmpty() ? "nothing yet" : String.join(", ", helpTopics)) + "\n\n" +
                "Reply in EXACTLY this format (no extra text):\n" +
                "ONE: <one sentence overall assessment>\n" +
                "THREE: <three sentence detailed summary covering strengths, weaknesses, and recommendation>\n" +
                "Keep it parent-friendly, encouraging, and actionable.";
    }

    private List<String> topHelpTopics(final Map<String, Object> profile) {
        Map<String, Object> concepts = safeMap(profile.get("concepts"));
        List<Map.Entry<String, Integer>> scored = new ArrayList<>();
        for (Map.Entry<String, Object> entry : concepts.entrySet()) {
            Map<String, Object> stats = safeMap(entry.getValue());
            int help = getInt(stats.get("helpRequests"));
            scored.add(Map.entry(entry.getKey(), help));
        }
        scored.sort((a, b) -> Integer.compare(b.getValue(), a.getValue()));
        List<String> results = new ArrayList<>();
        for (Map.Entry<String, Integer> entry : scored) {
            if (entry.getValue() <= 0) continue;
            results.add(entry.getKey());
            if (results.size() >= 3) break;
        }
        return results;
    }

    private void appendListLine(final StringBuilder builder, final String label, final List<String> values) {
        builder.append(label).append(": ");
        builder.append(values.isEmpty() ? "none yet" : String.join(", ", values));
        builder.append('\n');
    }

    private void appendRecentEventsLine(final StringBuilder builder, final Map<String, Object> profile) {
        Object recentValue = profile.get("recentEvents");
        if (!(recentValue instanceof List<?> recentEvents) || recentEvents.isEmpty()) {
            builder.append("Recent events: none yet");
            return;
        }

        List<String> compactEvents = new ArrayList<>();
        int start = Math.max(0, recentEvents.size() - 3);
        for (int i = start; i < recentEvents.size(); i++) {
            Object item = recentEvents.get(i);
            if (!(item instanceof Map<?, ?> rawMap)) {
                continue;
            }

            String type = rawMap.get("type") == null ? "unknown" : rawMap.get("type").toString();
            String topic = rawMap.get("topic") == null ? "general" : rawMap.get("topic").toString();
            String correctness = rawMap.get("correctness") == null ? "unknown" : rawMap.get("correctness").toString();
            String detail = rawMap.get("detail") == null ? "" : truncate(rawMap.get("detail").toString(), 80);
            compactEvents.add(type + " on " + topic + " (" + correctness + ")" + (detail.isBlank() ? "" : ": " + detail));
        }

        builder.append("Recent events: ");
        builder.append(compactEvents.isEmpty() ? "none yet" : String.join(" | ", compactEvents));
    }

    /**
     * Asks Groq to generate a personalized coding task for this child based on their weak spots.
     * Returns a map with keys: title, description, codeTemplate, language, pointValue.
     * Returns null if generation fails.
     */
    @Transactional(readOnly = true)
    public Map<String, String> generatePersonalizedTask(final Long childId, final String language) {
        if (childId == null) return null;

        Child child = childRepository.findById(childId).orElse(null);
        if (child == null) return null;

        String profileContext = buildAiHelpProfileContext(childId, language);
        boolean codeWorld = language != null && language.equalsIgnoreCase("codeworld");
        String safeLang = codeWorld ? "Mentora CodeWorld Python" : ((language != null && language.equals("cpp")) ? "C++" : "Python");

        String prompt;
        if (codeWorld) {
            prompt =
                "You are generating a personalized Mentora CodeWorld building challenge for a child (age 9-13) learning programming.\n" +
                "Student profile:\n" + profileContext + "\n\n" +
                "CodeWorld runs Python with this library: from mentora_world import *.\n" +
                "Available functions include cube, sphere, cylinder, capsule, panel, plane, move, rotate, scale, color, delete, clear, vector.\n" +
                "The code controls a 3D island by creating and modifying named objects.\n" +
                "Generate a world-building challenge that targets their weak areas, such as loops, variables, functions, conditions, coordinates, colors, or debugging.\n" +
                "The template must be valid CodeWorld Python base code, but it must not fully complete the challenge.\n" +
                "Use an almost-correct script, a partially finished script, or a skeleton with TODO comments depending on difficulty. The student must need to fix or add meaningful code before it passes.\n" +
                "The final solution should be solvable in 5-25 lines.\n" +
                "IMPORTANT: The completion checker requires the student to create an object named profile_goal and at least 3 objects total. Mention this in DESCRIPTION and include helpful base code.\n\n" +
                "Respond in EXACTLY this format. No markdown. No code fences. No ``` anywhere. Plain text only:\n" +
                "TITLE: [max 55 chars]\n" +
                "DESCRIPTION: [2-3 sentences: what the student must build or fix in CodeWorld]\n" +
                "TEMPLATE: [raw incomplete base CodeWorld Python only — NO backticks, NO ``` fences, just code lines]\n" +
                "EXPECTED: [one sentence: what the correct build should contain]\n" +
                "POINTS: [integer: 15, 20, 25, or 30]\n";
        } else {
            prompt =
                "You are generating a personalized " + safeLang + " coding challenge for a child (age 9-13) learning programming.\n" +
                "Student profile:\n" + profileContext + "\n\n" +
                "Generate a " + safeLang + " challenge that targets their specific weak areas. " +
                "It should be solvable in 5-20 lines of code.\n" +
                "The template must be base code only, not the complete answer. Include a small bug, missing line, TODO, or function body that the student must finish.\n\n" +
                "Respond in EXACTLY this format. No markdown. No code fences. No ``` anywhere. Plain text only:\n" +
                "TITLE: [max 55 chars]\n" +
                "DESCRIPTION: [2-3 sentences: what the student must write or fix]\n" +
                "TEMPLATE: [raw incomplete base source code only — NO backticks, NO ``` fences, just the code lines]\n" +
                "EXPECTED: [one sentence: what the correct output or behavior should be]\n" +
                "POINTS: [integer: 15, 20, 25, or 30]\n";
        }

        io.github.kawase.utility.GroqAI ai = new io.github.kawase.utility.GroqAI();
        String raw = ai.generate(prompt);
        if (raw == null || raw.isBlank() || raw.startsWith("AI Error")) return null;

        return parseGeneratedTask(raw, language);
    }

    private Map<String, String> parseGeneratedTask(final String raw, final String language) {
        Map<String, String> result = new java.util.LinkedHashMap<>();
        String[] markers = {"TITLE:", "DESCRIPTION:", "TEMPLATE:", "EXPECTED:", "POINTS:"};
        String[] keys    = {"title",  "description",  "codeTemplate", "expected", "pointValue"};

        for (int i = 0; i < markers.length; i++) {
            int start = raw.indexOf(markers[i]);
            if (start == -1) return null;
            start += markers[i].length();
            int end = raw.length();
            if (i + 1 < markers.length) {
                int next = raw.indexOf(markers[i + 1]);
                if (next != -1) end = next;
            }
            String value = raw.substring(start, end).trim();

            // Strip markdown code fences the LLM sometimes adds (```python ... ``` etc.)
            if (keys[i].equals("codeTemplate")) {
                value = stripCodeFences(value);
            }

            result.put(keys[i], value);
        }

        // Validate points is a sane integer
        try {
            int pts = Integer.parseInt(result.getOrDefault("pointValue", "20").replaceAll("[^0-9]", ""));
            pts = Math.max(15, Math.min(35, pts));
            result.put("pointValue", String.valueOf(pts));
        } catch (Exception e) {
            result.put("pointValue", "20");
        }

        result.put("language", language == null ? "python" : language);
        return result;
    }

    /**
     * Generates a short, profile-aware companion line for the robot NPC.
     * trigger examples: "greet", "challenge_fail", "challenge_success",
     * "idle", "entering_python", "entering_cpp", "task_complete", "hint_requested"
     * Returns a two-element array: [line, emotion] or null on failure.
     */
    private String stripCodeFences(final String value) {
        if (value == null) return "";
        String s = value.trim();
        // Remove opening fence: ```python, ```cpp, ```c++, ``` (with any language tag)
        s = s.replaceAll("(?m)^```[a-zA-Z+#]*\\s*\\n?", "");
        // Remove closing fence
        s = s.replaceAll("(?m)^```\\s*$", "");
        return s.trim();
    }

    @Transactional(readOnly = true)
    public String[] generateCompanionLine(final Long childId, final String trigger) {
        String profileContext = (childId != null) ? buildAiHelpProfileContext(childId, null) : "New student, no profile yet.";

        String emotion = emotionForTrigger(trigger);
        String prompt =
            "You are ARIA, a friendly robot companion inside a 3D educational game called Mentora.\n" +
            "You are talking to a child aged 9-13 who is learning to code.\n" +
            "Your personality: warm, a little witty, genuinely encouraging. You remember everything about this student.\n\n" +
            "Student profile:\n" + profileContext + "\n\n" +
            "Trigger event: \"" + (trigger == null ? "idle" : trigger) + "\"\n\n" +
            "Write ONE short line (max 20 words) that ARIA says OUT LOUD in response to this event.\n" +
            "Reference their actual stats if relevant (e.g. their streak, their struggles, their recent win).\n" +
            "Do NOT use quotes around the line. Do NOT write anything except the line itself.\n" +
            "Emotion to convey: " + emotion + ".";

        io.github.kawase.utility.GroqAI ai = new io.github.kawase.utility.GroqAI();
        String line = ai.generate(prompt);
        if (line == null || line.isBlank() || line.startsWith("AI Error")) {
            line = fallbackCompanionLine(trigger);
        }

        // Strip surrounding quotes if model added them
        line = line.trim().replaceAll("^[\"']|[\"']$", "");
        return new String[]{line, emotion};
    }

    @Transactional(readOnly = true)
    public String[] generateCompanionVoiceReply(final Long childId, final String transcript, final String context) {
        String cleanedTranscript = transcript == null ? "" : transcript.trim();
        if (cleanedTranscript.isBlank()) {
            return null;
        }

        if (isCheapCompanionVoiceNoise(cleanedTranscript, context)) {
            return null;
        }

        if (!shouldProcessCompanionVoiceIntent(cleanedTranscript, context)) {
            return null;
        }

        String profileContext = (childId != null) ? buildAiHelpProfileContext(childId, null) : "New student, no profile yet.";
        String executionContext = buildCompanionExecutionContext(cleanedTranscript, context);
        String prompt =
            "You are Rudolf, a friendly robot companion inside a 3D educational game called Mentora.\n" +
            "You are talking to a child aged 9-13 who is learning to code. Your answer appears in a speech bubble and may also be spoken aloud.\n" +
            "Your personality: warm, concise, a little witty, technically accurate, and useful.\n" +
            "Be a mentor, not an answer machine: teach the next step, ask a good follow-up when useful, and avoid dumping full solutions unless requested.\n\n" +
            "VERY IMPORTANT INTENT GATE:\n" +
            "The microphone may capture random speech that is not meant for Rudolf. First decide whether the student is actually trying to talk to Rudolf.\n" +
            "For passive single-player microphone input, be strict: do not answer unless the transcript directly addresses Rudolf/the robot or clearly asks for game, coding, debugging, or navigation help.\n" +
            "Respond only when one of these is true:\n" +
            "- The text directly addresses Rudolf/the robot/the mentor/bot, even if speech-to-text misspells the name.\n" +
            "- The context says conversation_active=true, meaning this is a follow-up in an ongoing Rudolf conversation.\n" +
            "- The student is clearly asking the in-game robot for help, guidance, code help, debugging, a route to an island, or an explanation.\n" +
            "Ignore when it is background chatter, self-talk, one-word noise, someone talking to another person, unrelated profanity, or text with no request for the game robot.\n" +
            "If ignored, output exactly ACTION: IGNORE and no line.\n\n" +
            "Student profile:\n" + profileContext + "\n\n" +
            "Voice interaction context: " + (context == null ? "general" : context) + "\n" +
            (executionContext.isBlank() ? "" : "Server code execution result:\n" + executionContext + "\n\n") +
            "The student just said: \"" + cleanedTranscript + "\"\n\n" +
            "Output format:\n" +
            "ACTION: RESPOND or IGNORE\n" +
            "EMOTION: happy, encouraging, concerned, excited, or thinking\n" +
            "LINE: your final line to say, or blank when ACTION is IGNORE\n\n" +
            "Response rules when ACTION is RESPOND:\n" +
            "- The LINE field must contain only Rudolf's spoken message. Never put ACTION, EMOTION, or LINE labels inside the spoken message.\n" +
            "- If recent conversation is provided in the context, use it to remember what the student asked and what you already answered.\n" +
            "- Treat the current student message as a follow-up when it refers to earlier turns with words like it, that, this, again, or why.\n" +
            "- You have access to a real server-side execution tool for Python and C++ snippets. It runs code with strict time/resource limits and returns stdout, stderr, exit code, and timeout status.\n" +
            "- If a server code execution result is provided, use the exact stdout/stderr/exit code to answer. Say briefly that you ran it.\n" +
            "- If the student asks you to run code but no runnable code is available, ask them to paste or open the code in the game instead of pretending you ran it.\n" +
            "- Reply directly to the student. Do not mention transcription, packets, prompts, or backend systems.\n" +
            "- For normal conversation, use 1-3 short sentences.\n" +
            "- If they ask for code, debugging, syntax, or an example, include a small fenced code block with a language tag, like ```python or ```cpp.\n" +
            "- If you include Python or C++ code that is meant to run, make it a complete runnable snippet when possible, because the server will execute generated code once before the final answer.\n" +
            "- When generated code execution results are supplied, revise your answer to include the observed output or error instead of guessing.\n" +
            "- Keep code snippets minimal: usually 3-10 lines. Prefer showing the exact pattern or fix, not a whole program.\n" +
            "- Before or after code, add one short sentence explaining why it works.\n" +
            "- Use Markdown only for code fences; avoid tables and long bullet lists because the game bubble is small.\n" +
            "- Maximum response length: 900 characters. If the task is bigger, give the first step and ask if they want the next part.";

        io.github.kawase.utility.GroqAI ai = new io.github.kawase.utility.GroqAI();
        String raw = ai.generate(prompt);
        if (raw == null || raw.isBlank() || raw.startsWith("AI Error")) {
            raw = "ACTION: RESPOND\nEMOTION: encouraging\nLINE: I heard you. Let's break it down one step at a time.";
        }

        String action = extractCompanionStructuredField(raw, "ACTION");
        if ("IGNORE".equalsIgnoreCase(action) || raw.trim().equalsIgnoreCase("ACTION: IGNORE")) {
            return null;
        }

        String emotion = extractCompanionStructuredField(raw, "EMOTION");
        if (emotion.isBlank()) {
            emotion = "encouraging";
        }

        String line = extractCompanionStructuredField(raw, "LINE");
        if (line.isBlank()) {
            line = raw == null ? "" : raw.trim();
        }

        if (line.equalsIgnoreCase("__IGNORE__") ||
            line.equalsIgnoreCase("IGNORE") ||
            line.toUpperCase().startsWith("ACTION: IGNORE")) {
            return null;
        }

        line = sanitizeCompanionSpokenLine(line);
        if (line.isBlank()) {
            return null;
        }

        String generatedExecutionContext = buildGeneratedCodeExecutionContext(line);
        if (!generatedExecutionContext.isBlank()) {
            String revisionPrompt = prompt + "\n\n" +
                "Your draft response was:\n" + line + "\n\n" +
                "The server executed the runnable code you included in that draft:\n" + generatedExecutionContext + "\n\n" +
                "Rewrite the final answer for the student. Keep the useful code if it helps, but include the observed stdout/stderr/exit code in plain language. " +
                "Do not claim a different output. Keep it under 900 characters. Return only the final spoken line, with no ACTION, EMOTION, or LINE labels.";
            String revisedLine = ai.generate(revisionPrompt);
            if (revisedLine != null && !revisedLine.isBlank() && !revisedLine.startsWith("AI Error")) {
                line = sanitizeCompanionSpokenLine(revisedLine);
            } else {
                line = sanitizeCompanionSpokenLine(appendExecutionSummary(line, generatedExecutionContext));
            }
        }

        line = sanitizeCompanionSpokenLine(line);
        if (line.isBlank()) {
            return null;
        }

        return new String[]{line, normalizeCompanionEmotion(emotion)};
    }

    private boolean isCheapCompanionVoiceNoise(final String transcript, final String context) {
        String normalized = transcript == null ? "" : transcript.trim().toLowerCase();
        if (normalized.length() < 3) {
            return true;
        }

        boolean activeConversation = context != null && context.toLowerCase().contains("conversation_active=true");
        if (activeConversation) {
            return false;
        }

        String compact = normalized.replaceAll("[^a-z0-9]+", " ").trim();
        return compact.equals("um") ||
            compact.equals("uh") ||
            compact.equals("ah") ||
            compact.equals("hmm") ||
            compact.equals("mmm") ||
            compact.equals("test") ||
            compact.equals("testing") ||
            compact.equals("hello") ||
            compact.equals("hi");
    }

    private boolean shouldProcessCompanionVoiceIntent(final String transcript, final String context) {
        String normalized = normalizeCompanionVoiceText(transcript);
        if (normalized.isBlank()) {
            return false;
        }

        boolean activeConversation = context != null && context.toLowerCase().contains("conversation_active=true");
        boolean addressedToCompanion = containsCompanionAddress(normalized);
        if (addressedToCompanion) {
            return true;
        }

        boolean mentionsIsland = normalized.contains(" island") ||
            normalized.contains("python") ||
            normalized.contains("c plus plus") ||
            normalized.contains("cplusplus") ||
            normalized.contains("cpp") ||
            normalized.contains("logic") ||
            normalized.contains("community");
        boolean asksForRoute = normalized.contains("take me") ||
            normalized.contains("guide me") ||
            normalized.contains("lead me") ||
            normalized.contains("show me") ||
            normalized.contains("bring me") ||
            normalized.contains("go to") ||
            normalized.contains("where is") ||
            normalized.contains("route to");
        if (mentionsIsland && asksForRoute) {
            return true;
        }

        if (isPassiveBackgroundSpeech(normalized)) {
            return false;
        }

        if (activeConversation) {
            return true;
        }

        return normalized.contains("help") ||
            normalized.contains("explain") ||
            normalized.contains("teach") ||
            normalized.contains("debug") ||
            normalized.contains("error") ||
            normalized.contains("fix") ||
            normalized.contains("code") ||
            normalized.contains("run this") ||
            normalized.contains("what is") ||
            normalized.contains("how do") ||
            normalized.contains("how can") ||
            normalized.contains("why does") ||
            normalized.contains("what does");
    }

    private boolean isPassiveBackgroundSpeech(final String normalized) {
        if (normalized.matches(".*\\bfuck\\s+you\\b.*")) {
            return true;
        }

        if ((normalized.contains("next one") || normalized.contains("next slide")) &&
            (normalized.contains("going to") || normalized.contains("go to") || normalized.contains("move on"))) {
            return true;
        }

        if (normalized.startsWith("i am going to ") || normalized.startsWith("im going to ")) {
            return true;
        }

        String[] words = normalized.split("\\s+");
        return words.length <= 5 &&
            !containsCompanionAddress(normalized) &&
            !normalized.contains("help") &&
            !normalized.contains("guide") &&
            !normalized.contains("take me") &&
            !normalized.contains("explain") &&
            !normalized.contains("what is") &&
            !normalized.contains("how do");
    }

    private boolean containsCompanionAddress(final String normalized) {
        return normalized.contains("rudolf") ||
            normalized.contains("rudolph") ||
            normalized.contains("rodolf") ||
            normalized.contains("hey robot") ||
            normalized.contains("the robot") ||
            normalized.contains("robot can") ||
            normalized.contains("robot please") ||
            normalized.contains("mentor") ||
            normalized.contains("bot");
    }

    private String normalizeCompanionVoiceText(final String transcript) {
        if (transcript == null) {
            return "";
        }

        return transcript
            .toLowerCase()
            .replace("c++", "c plus plus")
            .replaceAll("[^a-z0-9+]+", " ")
            .replaceAll("\\s+", " ")
            .trim();
    }

    private String extractCompanionStructuredField(final String raw, final String key) {
        if (raw == null || raw.isBlank() || key == null || key.isBlank()) {
            return "";
        }

        String marker = key + ":";
        String upper = raw.toUpperCase();
        int start = upper.indexOf(marker);
        if (start < 0) {
            return "";
        }

        start += marker.length();
        int end = raw.length();
        String[] nextMarkers = {"\nACTION:", "\nEMOTION:", "\nLINE:"};
        for (String nextMarker : nextMarkers) {
            int next = upper.indexOf(nextMarker, start);
            if (next >= 0 && next < end) {
                end = next;
            }
        }

        return raw.substring(start, end).trim();
    }

    private String normalizeCompanionEmotion(final String emotion) {
        if (emotion == null) {
            return "encouraging";
        }

        String normalized = emotion.trim().toLowerCase();
        return switch (normalized) {
            case "happy", "encouraging", "concerned", "excited", "thinking", "ignore" -> normalized;
            default -> "encouraging";
        };
    }

    private String sanitizeCompanionSpokenLine(final String rawLine) {
        if (rawLine == null || rawLine.isBlank()) {
            return "";
        }

        String line = rawLine.trim().replaceAll("^[\"']|[\"']$", "");
        String structuredLine = extractCompanionStructuredField(line, "LINE");
        if (!structuredLine.isBlank()) {
            line = structuredLine.trim();
        }

        line = line.replaceAll("(?im)^\\s*ACTION\\s*:\\s*.*(?:\\R|$)", "");
        line = line.replaceAll("(?im)^\\s*EMOTION\\s*:\\s*.*(?:\\R|$)", "");
        line = line.replaceAll("(?im)^\\s*LINE\\s*:\\s*", "");
        line = line.replaceAll("(?i)^\\s*[\\[(]?(happy|encouraging|concerned|excited|thinking)[\\])]?[\\s:—\\-]+", "");
        line = line.trim().replaceAll("^[\"']|[\"']$", "");
        return line.trim();
    }

    private String buildGeneratedCodeExecutionContext(final String generatedResponse) {
        String code = extractLastFencedCode(generatedResponse);
        if (code.isBlank() || !looksLikeGeneratedRunnableCode(generatedResponse, code)) {
            return "";
        }

        if (code.length() > 5000) {
            return "Rudolf generated code, but the snippet was too long to run safely in a quick voice reply.";
        }

        String language = inferExecutionLanguage(generatedResponse, code);
        if ("cpp".equals(language)) {
            CppExecutor.ExecutionResult result = CppExecutor.execute(code, 8);
            return formatExecutionResult("C++", result.output, result.error, result.exitCode, result.isTimeout);
        }

        PythonExecutor.ExecutionResult result = PythonExecutor.execute(code, 8);
        return formatExecutionResult("Python", result.output, result.error, result.exitCode, result.isTimeout);
    }

    private boolean looksLikeGeneratedRunnableCode(final String generatedResponse, final String code) {
        String response = generatedResponse == null ? "" : generatedResponse.toLowerCase();
        String snippet = code == null ? "" : code.toLowerCase();
        return response.contains("```python") ||
            response.contains("```py") ||
            response.contains("```cpp") ||
            response.contains("```c++") ||
            snippet.contains("print") ||
            snippet.contains("cout") ||
            snippet.contains("int main") ||
            snippet.contains("#include") ||
            snippet.contains("def ");
    }

    private String appendExecutionSummary(final String line, final String executionContext) {
        String output = extractExecutionField(executionContext, "stdout:");
        String error = extractExecutionField(executionContext, "stderr:");
        String summary = "I ran it. Output: " + (output.isBlank() ? "(no stdout)" : truncate(output, 160));
        if (!error.isBlank() && !"(no stderr)".equals(error)) {
            summary += " Error: " + truncate(error, 160);
        }

        return truncate(line + "\n\n" + summary, 900);
    }

    private String extractExecutionField(final String executionContext, final String marker) {
        if (executionContext == null || marker == null) {
            return "";
        }

        int start = executionContext.indexOf(marker);
        if (start < 0) {
            return "";
        }

        start += marker.length();
        int end = executionContext.length();
        if ("stdout:".equals(marker)) {
            int stderrIndex = executionContext.indexOf("\nstderr:", start);
            if (stderrIndex >= 0) {
                end = stderrIndex;
            }
        }

        return executionContext.substring(start, end).trim();
    }

    private String buildCompanionExecutionContext(final String transcript, final String context) {
        String combined = (transcript == null ? "" : transcript) + "\n" + (context == null ? "" : context);
        if (!looksLikeExecutionRequest(combined)) {
            return "";
        }

        String code = extractLastFencedCode(combined);
        if (code.isBlank()) {
            return "The student asked to execute code, but no fenced runnable code was available in the current conversation.";
        }

        if (code.length() > 5000) {
            return "The student asked to execute code, but the snippet is too long to run safely in a quick voice reply.";
        }

        String language = inferExecutionLanguage(combined, code);
        if ("cpp".equals(language)) {
            CppExecutor.ExecutionResult result = CppExecutor.execute(code, 8);
            return formatExecutionResult("C++", result.output, result.error, result.exitCode, result.isTimeout);
        }

        PythonExecutor.ExecutionResult result = PythonExecutor.execute(code, 8);
        return formatExecutionResult("Python", result.output, result.error, result.exitCode, result.isTimeout);
    }

    private boolean looksLikeExecutionRequest(final String text) {
        if (text == null) {
            return false;
        }

        String normalized = text.toLowerCase();
        boolean wantsRun =
            normalized.contains("run ") ||
            normalized.contains("execute") ||
            normalized.contains("test ") ||
            normalized.contains("try ") ||
            normalized.contains("output") ||
            normalized.contains("what does") ||
            normalized.contains("what will") ||
            normalized.contains("what prints") ||
            normalized.contains("compile");
        return wantsRun && (normalized.contains("```") || normalized.contains("print") || normalized.contains("cout") || normalized.contains("int main") || normalized.contains("def "));
    }

    private String extractLastFencedCode(final String text) {
        if (text == null || text.isBlank()) {
            return "";
        }

        String lastCode = "";
        int searchIndex = 0;
        while (true) {
            int start = text.indexOf("```", searchIndex);
            if (start < 0) {
                break;
            }

            int contentStart = text.indexOf('\n', start + 3);
            if (contentStart < 0) {
                break;
            }

            int end = text.indexOf("```", contentStart + 1);
            if (end < 0) {
                break;
            }

            String code = text.substring(contentStart + 1, end).trim();
            if (!code.isBlank()) {
                lastCode = code;
            }

            searchIndex = end + 3;
        }

        if (!lastCode.isBlank()) {
            return lastCode;
        }

        String normalized = text.trim();
        if (normalized.contains("print") || normalized.contains("cout") || normalized.contains("int main") || normalized.contains("def ")) {
            return normalized;
        }

        return "";
    }

    private String inferExecutionLanguage(final String text, final String code) {
        String normalized = ((text == null ? "" : text) + "\n" + (code == null ? "" : code)).toLowerCase();
        if (normalized.contains("c++") || normalized.contains("cpp") || normalized.contains("#include") || normalized.contains("std::") || normalized.contains("cout") || normalized.contains("int main")) {
            return "cpp";
        }

        return "python";
    }

    private String formatExecutionResult(final String language, final String output, final String error, final int exitCode, final boolean timeout) {
        String safeOutput = truncate(output == null || output.isBlank() ? "(no stdout)" : output.trim(), 1200);
        String safeError = truncate(error == null || error.isBlank() ? "(no stderr)" : error.trim(), 1200);
        return "Language: " + language + "\n" +
            "Exit code: " + exitCode + "\n" +
            "Timed out: " + timeout + "\n" +
            "stdout:\n" + safeOutput + "\n" +
            "stderr:\n" + safeError;
    }

    private String emotionForTrigger(final String trigger) {
        if (trigger == null) return "encouraging";
        return switch (trigger) {
            case "challenge_success", "task_complete" -> "excited";
            case "challenge_fail" -> "concerned";
            case "greet" -> "happy";
            case "entering_python", "entering_cpp", "entering_community" -> "thinking";
            case "hint_requested" -> "encouraging";
            default -> "encouraging";
        };
    }

    private String fallbackCompanionLine(final String trigger) {
        if (trigger == null) return "I'm right here if you need me!";
        return switch (trigger) {
            case "challenge_success", "task_complete" -> "Yes! You nailed it!";
            case "challenge_fail" -> "Don't give up — you're closer than you think.";
            case "greet" -> "Hey! Ready to write some code today?";
            case "entering_python" -> "Python island — my favourite!";
            case "entering_cpp" -> "C++ territory. This is where things get interesting.";
            case "entering_community" -> "Community courses ahead — let's see what others made.";
            case "hint_requested" -> "Good call asking for help — that's how you learn faster.";
            default -> "I'm right here if you need me!";
        };
    }

    private String deriveTopic(final String context, final String question) {
        String text = (context == null ? "" : context) + " " + (question == null ? "" : question);
        String normalized = text.toLowerCase();

        if (normalized.contains("loop") || normalized.contains("for ") || normalized.contains("while")) {
            return "loops";
        }
        if (normalized.contains("array") || normalized.contains("vector")) {
            return "arrays";
        }
        if (normalized.contains("pointer") || normalized.contains("address")) {
            return "pointers";
        }
        if (normalized.contains("reference") || normalized.contains("&")) {
            return "references";
        }
        if (normalized.contains("function") || normalized.contains("return")) {
            return "functions";
        }
        if (normalized.contains("if") || normalized.contains("else") || normalized.contains("switch")) {
            return "conditionals";
        }
        if (normalized.contains("class") || normalized.contains("struct") || normalized.contains("object")) {
            return "oop";
        }
        if (context != null && !context.isBlank()) {
            return context;
        }
        return "general";
    }

    private String deriveLanguage(final String topic, final String details) {
        String text = (topic == null ? "" : topic) + " " + (details == null ? "" : details);
        String normalized = text.toLowerCase();
        if (normalized.contains("cpp") || normalized.contains("c++")) {
            return "cpp";
        }
        if (normalized.contains("python") || normalized.startsWith("py_") || normalized.contains("py_")) {
            return "python";
        }
        if (normalized.startsWith("cpp:")) {
            return "cpp";
        }
        if (normalized.startsWith("python:") || normalized.startsWith("py:")) {
            return "python";
        }
        return null;
    }

    private void updateProfile(final Map<String, Object> gameStats, final String profileKey, final String eventType, final String topic, final int correctness, final String details) {
        Map<String, Object> aiProfile = ensureMap(gameStats, profileKey);
        Map<String, Object> topics = ensureMap(aiProfile, "topics");
        Map<String, Object> concepts = ensureMap(aiProfile, "concepts");
        Map<String, Object> mistakes = ensureMap(aiProfile, "mistakes");

        String resolvedTopic = (topic == null || topic.isBlank()) ? "general" : topic.trim();
        Map<String, Object> topicStats = ensureMap(topics, resolvedTopic);

        int totalInteractions = getInt(aiProfile.get("totalInteractions")) + 1;
        aiProfile.put("totalInteractions", totalInteractions);

        if ("ai_chat".equals(eventType)) {
            aiProfile.put("chatTurns", getInt(aiProfile.get("chatTurns")) + 1);
        } else if ("ai_hint".equals(eventType) || "hint".equals(eventType)) {
            aiProfile.put("hintsUsed", getInt(aiProfile.get("hintsUsed")) + 1);
        }

        if (correctness == 1) {
            aiProfile.put("correctCount", getInt(aiProfile.get("correctCount")) + 1);
            topicStats.put("correct", getInt(topicStats.get("correct")) + 1);
        } else if (correctness == 0) {
            aiProfile.put("incorrectCount", getInt(aiProfile.get("incorrectCount")) + 1);
            topicStats.put("incorrect", getInt(topicStats.get("incorrect")) + 1);
        }

        topicStats.put("attempts", getInt(topicStats.get("attempts")) + 1);
        topicStats.put("lastResult", correctness == 1 ? "correct" : correctness == 0 ? "incorrect" : "unknown");
        topicStats.put("lastSeen", Instant.now().toString());
        topics.put(resolvedTopic, topicStats);
        aiProfile.put("topics", topics);

        String concept = deriveConcept(resolvedTopic, details);
        if (concept != null) {
            Map<String, Object> conceptStats = ensureMap(concepts, concept);
            if (correctness == 1) {
                conceptStats.put("correct", getInt(conceptStats.get("correct")) + 1);
            } else if (correctness == 0) {
                conceptStats.put("incorrect", getInt(conceptStats.get("incorrect")) + 1);
            }
            if ("ai_chat".equals(eventType) || "ai_hint".equals(eventType)) {
                conceptStats.put("helpRequests", getInt(conceptStats.get("helpRequests")) + 1);
            }
            conceptStats.put("lastSeen", Instant.now().toString());
            concepts.put(concept, conceptStats);
        }

        String mistake = deriveMistake(details);
        if (mistake != null) {
            mistakes.put(mistake, getInt(mistakes.get(mistake)) + 1);
        }

        aiProfile.put("concepts", concepts);
        aiProfile.put("mistakes", mistakes);
        aiProfile.put("lastUpdated", Instant.now().toString());

        List<Map<String, Object>> recentEvents = ensureList(aiProfile, "recentEvents");
        Map<String, Object> event = new LinkedHashMap<>();
        event.put("ts", Instant.now().toString());
        event.put("type", eventType == null ? "unknown" : eventType);
        event.put("topic", resolvedTopic);
        event.put("correctness", correctness == 1 ? "correct" : correctness == 0 ? "incorrect" : "unknown");
        event.put("detail", truncate(details, 180));
        recentEvents.add(event);
        if (recentEvents.size() > 10) {
            recentEvents.remove(0);
        }
        aiProfile.put("recentEvents", recentEvents);

        gameStats.put(profileKey, aiProfile);
    }

    private String deriveConcept(final String topic, final String details) {
        String text = (topic == null ? "" : topic) + " " + (details == null ? "" : details);
        String normalized = text.toLowerCase();
        if (normalized.contains("loop") || normalized.contains("for ") || normalized.contains("while") || normalized.contains("range(")) {
            return "loops";
        }
        if (normalized.contains("if") || normalized.contains("else") || normalized.contains("switch") || normalized.contains("condition")) {
            return "conditionals";
        }
        if (normalized.contains("function") || normalized.contains("def ") || normalized.contains("return")) {
            return "functions";
        }
        if (normalized.contains("print") || normalized.contains("cout") || normalized.contains("output")) {
            return "output_formatting";
        }
        if (normalized.contains("list") || normalized.contains("array") || normalized.contains("vector")) {
            return "collections";
        }
        if (normalized.contains("string") || normalized.contains("char")) {
            return "strings";
        }
        if (normalized.contains("operator") || normalized.contains("+") || normalized.contains("-") || normalized.contains("*") || normalized.contains("/")) {
            return "operators";
        }
        if (normalized.contains("syntax") || normalized.contains("indentation") || normalized.contains("expected")) {
            return "syntax";
        }
        if (normalized.contains("error") || normalized.contains("exception")) {
            return "debugging";
        }
        return "general";
    }

    private String deriveMistake(final String details) {
        if (details == null) {
            return null;
        }
        String normalized = details.toLowerCase();
        if (normalized.contains("syntax") || normalized.contains("indentation")) {
            return "syntax error";
        }
        if (normalized.contains("timeout") || normalized.contains("timed out")) {
            return "infinite loop or slow code";
        }
        if (normalized.contains("typeerror") || normalized.contains("type mismatch")) {
            return "type mismatch";
        }
        if (normalized.contains("nameerror") || normalized.contains("undefined")) {
            return "undefined variable";
        }
        if (normalized.contains("output")) {
            return "wrong output format";
        }
        return null;
    }

    @SuppressWarnings("unchecked")
    private Map<String, Object> ensureMap(final Map<String, Object> parent, final String key) {
        Object value = parent.get(key);
        if (value instanceof Map) {
            return (Map<String, Object>) value;
        }
        Map<String, Object> created = new HashMap<>();
        parent.put(key, created);
        return created;
    }

    @SuppressWarnings("unchecked")
    private Map<String, Object> safeMap(final Object value) {
        if (value instanceof Map) {
            return (Map<String, Object>) value;
        }
        return Collections.emptyMap();
    }

    @SuppressWarnings("unchecked")
    private List<Map<String, Object>> ensureList(final Map<String, Object> parent, final String key) {
        Object value = parent.get(key);
        if (value instanceof List) {
            return (List<Map<String, Object>>) value;
        }
        List<Map<String, Object>> created = new ArrayList<>();
        parent.put(key, created);
        return created;
    }

    private int getInt(final Object value) {
        if (value instanceof Number) {
            return ((Number) value).intValue();
        }
        return 0;
    }

    private String truncate(final String value, final int max) {
        if (value == null) {
            return "";
        }
        String trimmed = value.trim();
        if (trimmed.length() <= max) {
            return trimmed;
        }
        return trimmed.substring(0, max);
    }
}
