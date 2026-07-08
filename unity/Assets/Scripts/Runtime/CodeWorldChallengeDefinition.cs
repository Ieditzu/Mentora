using System;
using System.Collections.Generic;
using UnityEngine;

public enum CodeWorldChallengeRequirementKind
{
    ObjectExists,
    ObjectMissing,
    ObjectNear,
    ScaleAtLeast,
    ColorNear,
    ObjectCountAtLeast,
    PrefixCountAtLeast
}

[Serializable]
public sealed class CodeWorldChallengeRequirement
{
    public CodeWorldChallengeRequirementKind Kind;
    public string Label;
    public string ObjectName;
    public string Prefix;
    public Vector3 VectorValue;
    public Color ColorValue = Color.white;
    public float Tolerance = 0.5f;
    public int Count = 1;

    public static CodeWorldChallengeRequirement Exists(string objectName, string label)
    {
        return new CodeWorldChallengeRequirement
        {
            Kind = CodeWorldChallengeRequirementKind.ObjectExists,
            ObjectName = objectName,
            Label = label
        };
    }

    public static CodeWorldChallengeRequirement Missing(string objectName, string label)
    {
        return new CodeWorldChallengeRequirement
        {
            Kind = CodeWorldChallengeRequirementKind.ObjectMissing,
            ObjectName = objectName,
            Label = label
        };
    }

    public static CodeWorldChallengeRequirement Near(string objectName, Vector3 position, float tolerance, string label)
    {
        return new CodeWorldChallengeRequirement
        {
            Kind = CodeWorldChallengeRequirementKind.ObjectNear,
            ObjectName = objectName,
            VectorValue = position,
            Tolerance = tolerance,
            Label = label
        };
    }

    public static CodeWorldChallengeRequirement ScaleAtLeast(string objectName, Vector3 scale, string label)
    {
        return new CodeWorldChallengeRequirement
        {
            Kind = CodeWorldChallengeRequirementKind.ScaleAtLeast,
            ObjectName = objectName,
            VectorValue = scale,
            Label = label
        };
    }

    public static CodeWorldChallengeRequirement ColorNear(string objectName, Color color, float tolerance, string label)
    {
        return new CodeWorldChallengeRequirement
        {
            Kind = CodeWorldChallengeRequirementKind.ColorNear,
            ObjectName = objectName,
            ColorValue = color,
            Tolerance = tolerance,
            Label = label
        };
    }

    public static CodeWorldChallengeRequirement CountAtLeast(int count, string label)
    {
        return new CodeWorldChallengeRequirement
        {
            Kind = CodeWorldChallengeRequirementKind.ObjectCountAtLeast,
            Count = count,
            Label = label
        };
    }

    public static CodeWorldChallengeRequirement PrefixCountAtLeast(string prefix, int count, string label)
    {
        return new CodeWorldChallengeRequirement
        {
            Kind = CodeWorldChallengeRequirementKind.PrefixCountAtLeast,
            Prefix = prefix,
            Count = count,
            Label = label
        };
    }
}

[Serializable]
public sealed class CodeWorldChallengeDefinition
{
    public string Id;
    public string Title;
    public string Description;
    public string StarterCode;
    public string[] SetupCommands = Array.Empty<string>();
    public readonly List<CodeWorldChallengeRequirement> Requirements = new List<CodeWorldChallengeRequirement>();
    public bool GeneratedByAi;
}
