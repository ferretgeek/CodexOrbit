namespace CodexQuota.Services;

public sealed class MiniViewState
{
	public bool ShowPill;

	public string PillLabel;

	public string PillValue;

	public string PillReset;

	public AlertLevel PillAlert;

	public bool ShowPlanBadge;

	public PlanKind PlanKind;

	public string PlanTitle;

	public string PlanSubtitle;

	public string PlanGlyph;

	public bool GaugeValid;

	public double GaugePercent;

	public string GaugeValue;

	public string GaugeLabel;

	public string GaugeReset;

	public bool GaugeIsWeek;

	public AlertLevel GaugeAlert;

	public string TipStatus;

	public bool TipIsOk;

	public bool TipIsWarnTone;

	public string TipDetail;
}
