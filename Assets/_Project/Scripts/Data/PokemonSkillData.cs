using System;
using UnityEngine;

public enum SkillTargetType
{
    Single,
    Area,
    Line,
    All
}

[Serializable]
public class PokemonSkillData
{
    public string skillName;

    [TextArea]
    public string description;

    public float damage;
    public int manaCost;
    public SkillTargetType targetType;

    /// <summary>targetType == Area 일 때만 사용 (칸 수)</summary>
    public int areaRadius;

    /// <summary>targetType == Line 일 때만 사용 (칸 수)</summary>
    public int lineLength;
}
