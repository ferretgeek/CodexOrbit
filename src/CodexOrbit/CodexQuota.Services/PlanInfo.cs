using System;

namespace CodexQuota.Services;

public static class PlanInfo
{
	public const string OverrideAuto = "auto";

	public const string OverrideFree = "free";

	public const string OverridePlus = "plus";

	public const string OverridePro5x = "pro5x";

	public const string OverridePro20x = "pro20x";

	public const string OverrideBusiness = "business";

	public const string OverrideGo = "go";

	public const string OverrideEnterprise = "enterprise";

	public const string DefaultProEdition = "pro20x";

	public static PlanBadge Resolve(string planTypeFromLog, string planOverride)
	{
		return FromKind(ResolveKind(planTypeFromLog, planOverride));
	}

	public static PlanKind ResolveKind(string planTypeFromLog, string planOverride)
	{
		if (!string.IsNullOrWhiteSpace(planOverride) && !string.Equals(planOverride, "auto", StringComparison.OrdinalIgnoreCase))
		{
			return ParseKind(planOverride);
		}
		PlanKind planKind = ParseKind(planTypeFromLog);
		if (planKind == PlanKind.Pro)
		{
			return ParseKind("pro20x");
		}
		return planKind;
	}

	public static PlanKind ParseKind(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
		{
			return PlanKind.Unknown;
		}
		string text = raw.Trim().ToLowerInvariant().Replace(" ", "")
			.Replace("_", "")
			.Replace("-", "")
			.Replace("×", "x")
			.Replace("chatgpt", "");
		switch (text)
		{
		case "free":
		case "freeplan":
			return PlanKind.Free;
		case "go":
			return PlanKind.Go;
		case "plus":
		case "chatgptplus":
			return PlanKind.Plus;
		case "business":
		case "team":
			return PlanKind.Business;
		case "enterprise":
		case "edu":
		case "enterpriseedu":
		case "ent26":
			return PlanKind.Enterprise;
		case "pro20x":
		case "pro20":
		case "20x":
		case "promax":
		case "proultra":
			return PlanKind.Pro20x;
		case "pro5x":
		case "pro5":
		case "5x":
		case "prolite":
			return PlanKind.Pro5x;
		case "pro":
		case "chatgptpro":
			return PlanKind.Pro;
		default:
			if (text.Contains("20x") || text.Contains("pro20"))
			{
				return PlanKind.Pro20x;
			}
			if (text.Contains("5x") || text.Contains("pro5"))
			{
				return PlanKind.Pro5x;
			}
			if (text.Contains("plus"))
			{
				return PlanKind.Plus;
			}
			if (text.Contains("free"))
			{
				return PlanKind.Free;
			}
			if (text.Contains("business"))
			{
				return PlanKind.Business;
			}
			if (text.Contains("enterprise"))
			{
				return PlanKind.Enterprise;
			}
			if (text.Contains("pro"))
			{
				return PlanKind.Pro;
			}
			return PlanKind.Unknown;
		}
	}

	public static PlanBadge FromKind(PlanKind kind)
	{
		return kind switch
		{
			PlanKind.Free => new PlanBadge
			{
				Kind = kind,
				Code = "free",
				Title = "FREE",
				Subtitle = "免费档",
				Glyph = "\ue716",
				ShowBadge = true
			},
			PlanKind.Go => new PlanBadge
			{
				Kind = kind,
				Code = "go",
				Title = "GO",
				Subtitle = "入门档",
				Glyph = "\ue945",
				ShowBadge = true
			},
			PlanKind.Plus => new PlanBadge
			{
				Kind = kind,
				Code = "plus",
				Title = "PLUS",
				Subtitle = "Plus 会员",
				Glyph = "\ue8fb",
				ShowBadge = true
			},
			PlanKind.Pro5x => new PlanBadge
			{
				Kind = kind,
				Code = "pro5x",
				Title = "PRO 5×",
				Subtitle = "专业 · 5 倍",
				Glyph = "\ue735",
				ShowBadge = true
			},
			PlanKind.Pro20x => new PlanBadge
			{
				Kind = kind,
				Code = "pro20x",
				Title = "PRO 20×",
				Subtitle = "至尊 · 20 倍",
				Glyph = "\ue734",
				ShowBadge = true
			},
			PlanKind.Pro => new PlanBadge
			{
				Kind = kind,
				Code = "pro",
				Title = "PRO",
				Subtitle = "专业档",
				Glyph = "\ue735",
				ShowBadge = true
			},
			PlanKind.Business => new PlanBadge
			{
				Kind = kind,
				Code = "business",
				Title = "BIZ",
				Subtitle = "商业版",
				Glyph = "\ue80f",
				ShowBadge = true
			},
			PlanKind.Enterprise => new PlanBadge
			{
				Kind = kind,
				Code = "enterprise",
				Title = "ENT",
				Subtitle = "企业版",
				Glyph = "\ue821",
				ShowBadge = true
			},
			_ => new PlanBadge
			{
				Kind = PlanKind.Unknown,
				Code = "",
				Title = "",
				Subtitle = "",
				Glyph = "",
				ShowBadge = false
			},
		};
	}

	public static string KindToOverride(PlanKind kind)
	{
		return kind switch
		{
			PlanKind.Free => "free",
			PlanKind.Go => "go",
			PlanKind.Plus => "plus",
			PlanKind.Pro5x => "pro5x",
			PlanKind.Pro20x => "pro20x",
			PlanKind.Business => "business",
			PlanKind.Enterprise => "enterprise",
			_ => "auto",
		};
	}
}
