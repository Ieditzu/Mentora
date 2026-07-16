using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public sealed class MachineLearningProblemCatalog
{
    public MachineLearningProblem[] problems = Array.Empty<MachineLearningProblem>();
}

[Serializable]
public sealed class MachineLearningProblem
{
    public string slug;
    public string title;
    public string description;
    public string hint;
    public string difficulty;
    public string[] concepts = Array.Empty<string>();
    public string starterCode;
    public string datasetPreview;
    public string[] datasetColumns = Array.Empty<string>();
    public int rewardPoints;
    public int attemptCount;
    public float bestScore;
    public bool completed;

    // Optional localized additions. Older servers can omit them safely.
    public string titleRo;
    public string descriptionRo;
    public string hintRo;

    public string LocalizedTitle => SelectLocalized(title, titleRo);
    public string LocalizedDescription => SelectLocalized(description, descriptionRo);
    public string LocalizedHint => SelectLocalized(hint, hintRo);

    private static string SelectLocalized(string english, string romanian)
    {
        if (MentoraLocalization.IsRomanian && !string.IsNullOrWhiteSpace(romanian))
        {
            return romanian;
        }

        return english ?? string.Empty;
    }
}

[Serializable]
public sealed class MachineLearningSubmissionResult
{
    public string problemSlug;
    public bool passed;
    public float score;
    public string metricName;
    public float metricValue;
    public float threshold;
    public string feedback;
    public string stdout;
    public string error;
    public int attemptCount;
    public float bestScore;
    public bool completed;
    public bool rewardGranted;
    public int rewardPoints;
    public int totalPoints;
    public bool infrastructureError;
}

/// <summary>
/// Tolerant JSON boundary for the ML catalog and submission response. JsonUtility is used so
/// this remains compatible with IL2CPP builds; unknown server fields are intentionally ignored.
/// </summary>
public static class MachineLearningJson
{
    private static readonly Regex LegacyInfrastructureError = new Regex(
        "\\\"infrastructureError\\\"\\s*:\\s*\\\"(?<message>(?:\\\\.|[^\\\"])*)\\\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryParseCatalog(string json, out MachineLearningProblemCatalog catalog, out string error)
    {
        catalog = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The server returned an empty problem catalog.";
            return false;
        }

        try
        {
            string normalized = json.Trim();
            if (normalized.StartsWith("[", StringComparison.Ordinal))
            {
                normalized = "{\"problems\":" + normalized + "}";
            }

            catalog = JsonUtility.FromJson<MachineLearningProblemCatalog>(normalized);
            if (catalog == null || catalog.problems == null)
            {
                error = "The problem catalog has an invalid shape.";
                return false;
            }

            var valid = new List<MachineLearningProblem>(catalog.problems.Length);
            for (int i = 0; i < catalog.problems.Length; i++)
            {
                MachineLearningProblem problem = catalog.problems[i];
                if (problem == null || string.IsNullOrWhiteSpace(problem.slug))
                {
                    continue;
                }

                problem.concepts = problem.concepts ?? Array.Empty<string>();
                problem.datasetColumns = problem.datasetColumns ?? Array.Empty<string>();
                valid.Add(problem);
            }

            catalog.problems = valid.ToArray();
            return true;
        }
        catch (Exception exception)
        {
            error = "The problem catalog could not be read: " + exception.Message;
            catalog = null;
            return false;
        }
    }

    public static bool TryParseResult(string json, out MachineLearningSubmissionResult result, out string error)
    {
        result = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The server returned an empty grading result.";
            return false;
        }

        try
        {
            string normalized = json.Trim();
            Match legacyMatch = LegacyInfrastructureError.Match(normalized);
            string legacyMessage = string.Empty;
            if (legacyMatch.Success)
            {
                legacyMessage = Regex.Unescape(legacyMatch.Groups["message"].Value);
                normalized = LegacyInfrastructureError.Replace(normalized, "\"infrastructureError\":true", 1);
            }

            result = JsonUtility.FromJson<MachineLearningSubmissionResult>(normalized);
            if (result == null)
            {
                error = "The grading result has an invalid shape.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(legacyMessage))
            {
                result.infrastructureError = true;
                if (string.IsNullOrWhiteSpace(result.error))
                {
                    result.error = legacyMessage;
                }
            }

            return true;
        }
        catch (Exception exception)
        {
            error = "The grading result could not be read: " + exception.Message;
            result = null;
            return false;
        }
    }
}
