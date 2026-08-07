namespace CodexQuota.Models;

public sealed class UsageSnapshot
{
	public UsageWindowSnapshot ShortWindow { get; set; }

	public UsageWindowSnapshot WeekWindow { get; set; }

	public string StatusMessage { get; set; }

	public string PlanType { get; set; }

	public bool IsLive { get; set; }

	public bool HasAnyData
	{
		get
		{
			if (ShortWindow == null)
			{
				return WeekWindow != null;
			}
			return true;
		}
	}
}
