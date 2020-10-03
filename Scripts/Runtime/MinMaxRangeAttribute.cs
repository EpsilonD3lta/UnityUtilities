using System;

public class MinMaxRangeAttribute : Attribute
{
    public MinMaxRangeAttribute(float min, float max)
    {
        Min = min;
        Max = max;
    }
    public float Min { get; private set; }
    public float Max { get; private set; }
}

[Serializable]
public struct MinMaxFloat
{
    public MinMaxFloat(float min, float max)
    {
        this.min = min;
        this.max = max;
    }

    public float min;
    public float max;
}