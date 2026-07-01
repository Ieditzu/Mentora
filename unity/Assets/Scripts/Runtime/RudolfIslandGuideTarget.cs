using UnityEngine;

public sealed class RudolfIslandGuideTarget : MonoBehaviour
{
    public enum IslandId
    {
        Community,
        Logic,
        Python,
        Cpp
    }

    [SerializeField] private IslandId island = IslandId.Python;
    [SerializeField] private string displayName = "";
    [SerializeField] private float hoverOffset = 4.2f;
    [SerializeField] private float arrivalRadius = 7f;

    public IslandId Island
    {
        get
        {
            IslandId parsed;
            if (TryParse(gameObject.name, out parsed))
            {
                return parsed;
            }

            return island;
        }
    }
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? GetDisplayName(Island) : displayName;
    public float ArrivalRadius => Mathf.Max(2f, arrivalRadius);

    public Vector3 GetGuidePosition(float extraBob)
    {
        return transform.position + Vector3.up * (hoverOffset + extraBob);
    }

    public static RudolfIslandGuideTarget Find(IslandId islandId)
    {
        RudolfIslandGuideTarget[] targets = FindObjectsOfType<RudolfIslandGuideTarget>(true);
        for (int i = 0; i < targets.Length; i++)
        {
            RudolfIslandGuideTarget target = targets[i];
            if (target != null && target.Island == islandId)
            {
                return target;
            }
        }

        return null;
    }

    public static bool TryParse(string text, out IslandId islandId)
    {
        islandId = IslandId.Python;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string lower = text.ToLowerInvariant();
        string normalized = NormalizeForMatching(lower);

        if (normalized.Contains("community"))
        {
            islandId = IslandId.Community;
            return true;
        }

        if (normalized.Contains("logic") || normalized.Contains("difficulty"))
        {
            islandId = IslandId.Logic;
            return true;
        }

        if (normalized.Contains("python") ||
            normalized.Contains("py island") ||
            normalized.Contains("piton") ||
            normalized.Contains("pithon") ||
            normalized.Contains("pythong") ||
            normalized.Contains("pie thon") ||
            normalized.Contains("pie ton") ||
            normalized.Contains("high thon"))
        {
            islandId = IslandId.Python;
            return true;
        }

        if (lower.Contains("c++") ||
            normalized.Contains("cpp") ||
            normalized.Contains("cplusplus") ||
            normalized.Contains("c plus plus") ||
            normalized.Contains("c island"))
        {
            islandId = IslandId.Cpp;
            return true;
        }

        return false;
    }

    public static string GetDisplayName(IslandId islandId)
    {
        switch (islandId)
        {
            case IslandId.Community:
                return "Community Island";
            case IslandId.Logic:
                return "Logic Island";
            case IslandId.Cpp:
                return "C++ Island";
            default:
                return "Python Island";
        }
    }

    private static string NormalizeForMatching(string value)
    {
        char[] chars = value.Replace("+", " plus ").ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (!char.IsLetterOrDigit(c))
            {
                chars[i] = ' ';
            }
        }

        string normalized = new string(chars);
        while (normalized.Contains("  "))
        {
            normalized = normalized.Replace("  ", " ");
        }

        return normalized.Trim();
    }
}
