using System;

namespace CodexQuota.Models;

public sealed class UsageWindowSnapshot
{
	public int WindowMinutes { get; set; }

	public double UsedPercent { get; set; }

	public DateTimeOffset ResetsAt { get; set; }

	public DateTimeOffset ObservedAt { get; set; }

	public string SourceFile { get; set; }

	public string LimitId { get; set; }

	public string LimitName { get; set; }

	public double RemainingPercent => Math.Max(0.0, Math.Min(100.0, 100.0 - UsedPercent));

	public bool IsUnusedInCurrentWindow => UsedPercent <= 0.0;

	public bool IsExpired(DateTimeOffset now)
	{
		return ResetsAt <= now;
	}
}
