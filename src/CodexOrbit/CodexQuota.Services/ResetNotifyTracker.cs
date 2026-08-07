using System;
using CodexQuota.Models;

namespace CodexQuota.Services;

public static class ResetNotifyTracker
{
	public static bool Evaluate(UsageWindowSnapshot window, DateTimeOffset now, ref long trackedResetsAtTicks, long notifiedDeadlineTicks, out long notifyDeadlineTicks, out bool trackingChanged)
	{
		trackingChanged = false;
		notifyDeadlineTicks = 0L;
		long num = trackedResetsAtTicks;
		if (window != null && num > 0 && notifiedDeadlineTicks != num && window.ObservedAt.UtcTicks >= num && window.ResetsAt.UtcTicks > num)
		{
			notifyDeadlineTicks = num;
		}
		if (window != null && window.ResetsAt > now)
		{
			long utcTicks = window.ResetsAt.UtcTicks;
			bool flag = string.Equals(window.LimitId, "codex", StringComparison.OrdinalIgnoreCase) && utcTicks < trackedResetsAtTicks;
			if ((trackedResetsAtTicks <= 0 || utcTicks > trackedResetsAtTicks) | flag)
			{
				trackedResetsAtTicks = utcTicks;
				trackingChanged = true;
			}
		}
		return notifyDeadlineTicks > 0;
	}
}
