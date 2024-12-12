namespace TomatoClock;

[Serializable]
public sealed class TomatoConfig
{
    public int Interval { get; set; } = 45;
    public int OffTimeHour { get; set; } = 18;
    public int OffTimeMinute { get; set; } = 0;
}
