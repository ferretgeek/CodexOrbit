using System;
using System.Globalization;
using CodexQuota.Models;

namespace CodexQuota.Services;

public static class UsageFormatter
{
	public static string DisplayLabel(UsageWindowSnapshot window)
	{
		if (window == null)
		{
			return "--";
		}
		if (!string.IsNullOrWhiteSpace(window.LimitName) && !string.Equals(window.LimitId, "codex", StringComparison.OrdinalIgnoreCase))
		{
			string[] parts = window.LimitName.Split(new char[1] { '-' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length > 0)
			{
				string text = parts[parts.Length - 1].Trim();
				if (text.Length > 0 && text.Length <= 10)
				{
					return text;
				}
			}
			return (window.LimitName.Length <= 10) ? window.LimitName : (window.LimitName.Substring(0, 9) + "…");
		}
		return WindowLabel(window.WindowMinutes);
	}

	public static string WindowLabel(int minutes)
	{
		if (minutes <= 0)
		{
			return "--";
		}
		if (minutes <= 1440)
		{
			if (minutes % 60 == 0)
			{
				return (minutes / 60).ToString(CultureInfo.InvariantCulture) + "h";
			}
			return minutes.ToString(CultureInfo.InvariantCulture) + "m";
		}
		int val = (int)Math.Round((double)minutes / 1440.0);
		return Math.Max(1, val).ToString(CultureInfo.InvariantCulture) + "d";
	}

	public static string CountdownLong(TimeSpan remaining)
	{
		if (remaining <= TimeSpan.Zero)
		{
			return "即将重置";
		}
		if (remaining.TotalDays >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}天{1}时后", (int)remaining.TotalDays, remaining.Hours);
		}
		if (remaining.TotalHours >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}时{1}分后", (int)remaining.TotalHours, remaining.Minutes);
		}
		if (remaining.TotalMinutes >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}分{1}秒后", remaining.Minutes, remaining.Seconds);
		}
		return string.Format(CultureInfo.InvariantCulture, "{0}秒后", Math.Max(1, remaining.Seconds));
	}

	public static string CountdownCompact(TimeSpan remaining)
	{
		if (remaining <= TimeSpan.Zero)
		{
			return "0m";
		}
		if (remaining.TotalDays >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}d{1}h", (int)remaining.TotalDays, remaining.Hours);
		}
		if (remaining.TotalHours >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}h{1}m", (int)remaining.TotalHours, remaining.Minutes);
		}
		if (remaining.TotalMinutes >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}m", Math.Max(1, remaining.Minutes));
		}
		return string.Format(CultureInfo.InvariantCulture, "{0}s", Math.Max(1, remaining.Seconds));
	}

	public static string CountdownRemaining(TimeSpan remaining)
	{
		if (remaining <= TimeSpan.Zero)
		{
			return "即将重置";
		}
		if (remaining.TotalDays >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "还剩 {0} 天 {1} 小时", (int)remaining.TotalDays, remaining.Hours);
		}
		if (remaining.TotalHours >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "还剩 {0} 小时 {1} 分", (int)remaining.TotalHours, remaining.Minutes);
		}
		if (remaining.TotalMinutes >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "还剩 {0} 分钟", Math.Max(1, (int)remaining.TotalMinutes));
		}
		return "不到 1 分钟";
	}

	public static string CountdownRemainingCompact(TimeSpan remaining)
	{
		if (remaining <= TimeSpan.Zero)
		{
			return "即将重置";
		}
		if (remaining.TotalDays >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}天{1}小时", (int)remaining.TotalDays, remaining.Hours);
		}
		if (remaining.TotalHours >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}小时{1}分", (int)remaining.TotalHours, remaining.Minutes);
		}
		if (remaining.TotalMinutes >= 1.0)
		{
			return string.Format(CultureInfo.InvariantCulture, "{0}分钟", Math.Max(1, (int)remaining.TotalMinutes));
		}
		return "<1分钟";
	}

	public static string WindowDetail(WindowDisplay display, DateTimeOffset now)
	{
		if (!display.Exists)
		{
			return "未检测到数据";
		}
		if (display.Stage == QuotaStage.Stale)
		{
			return display.RoundedPercent.ToString(CultureInfo.InvariantCulture) + "% 上次快照 · 等待同步";
		}
		if (display.IsUnused)
		{
			return "100% 剩余 · 新周期";
		}
		return display.RoundedPercent.ToString(CultureInfo.InvariantCulture) + "% 剩余 · " + CountdownLong(display.Source.ResetsAt - now) + "重置";
	}

	public static string SnapshotTime(DateTimeOffset observedAt, DateTimeOffset now)
	{
		DateTime localDateTime = observedAt.LocalDateTime;
		if (!(localDateTime.Date == now.LocalDateTime.Date))
		{
			return localDateTime.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
		}
		return "今天 " + localDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
	}

	public static bool IsMeaningfulCycleAdvance(UsageWindowSnapshot window, DateTimeOffset previousResetsAt)
	{
		if (window == null)
		{
			return false;
		}
		TimeSpan timeSpan = window.ResetsAt - previousResetsAt;
		if (timeSpan <= TimeSpan.Zero)
		{
			return false;
		}
		TimeSpan timeSpan2 = ((window.WindowMinutes <= 1440) ? TimeSpan.FromMinutes(Math.Max(30.0, (double)window.WindowMinutes / 4.0)) : TimeSpan.FromHours(12.0));
		return timeSpan >= timeSpan2;
	}
}
