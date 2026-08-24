namespace Lis.Cattle.Models;

public class CtsPollingJobOptions
{
    public const string SectionName = "CtsPollingJob";

    public bool Enabled { get; set; } = true;

    public string? CronSchedule { get; set; }

    public int PollingIntervalSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 10;
}
