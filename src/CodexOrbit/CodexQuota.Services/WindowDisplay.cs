using System;
using CodexQuota.Models;

namespace CodexQuota.Services;

public sealed class WindowDisplay
{
	public bool Exists;

	public QuotaStage Stage;

	public AlertLevel Alert;

	public double Percent;

	public bool IsUnused;

	public string Label;

	public UsageWindowSnapshot Source;

	public const double WarnThreshold = 20.0;

	public const double DangerThreshold = 8.0;

	public int RoundedPercent => (int)Math.Round(Percent);

	public static WindowDisplay From(UsageWindowSnapshot window, DateTimeOffset now)
	{
		return From(window, now, hideWhenStale: false);
	}

	public static WindowDisplay FromShort(UsageWindowSnapshot window, DateTimeOffset now)
	{
		return From(window, now, hideWhenStale: true);
	}

	private static WindowDisplay From(UsageWindowSnapshot window, DateTimeOffset now, bool hideWhenStale)
	{
		if (window == null)
		{
			return new WindowDisplay
			{
				Exists = false,
				Stage = QuotaStage.Missing,
				Alert = AlertLevel.Unknown
			};
		}
		bool flag = window.IsExpired(now);
		if (flag & hideWhenStale)
		{
			return new WindowDisplay
			{
				Exists = false,
				Stage = QuotaStage.Missing,
				Alert = AlertLevel.Unknown
			};
		}
		double remainingPercent = window.RemainingPercent;
		bool flag2 = !flag && window.IsUnusedInCurrentWindow;
		AlertLevel alert = ((flag | flag2) ? AlertLevel.Normal : ((remainingPercent <= 8.0) ? AlertLevel.Danger : ((!(remainingPercent <= 20.0)) ? AlertLevel.Normal : AlertLevel.Warn)));
		return new WindowDisplay
		{
			Exists = true,
			Stage = ((!flag) ? QuotaStage.Fresh : QuotaStage.Stale),
			Alert = alert,
			Percent = remainingPercent,
			IsUnused = flag2,
			Label = UsageFormatter.DisplayLabel(window),
			Source = window
		};
	}
}
