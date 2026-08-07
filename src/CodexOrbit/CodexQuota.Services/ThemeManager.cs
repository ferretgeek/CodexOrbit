using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace CodexQuota.Services;

public static class ThemeManager
{
	private static IList<ThemePalette> _presets;

	public static ThemePalette Current { get; private set; }

	public static IList<ThemePalette> Presets
	{
		get
		{
			if (_presets == null)
			{
				_presets = BuildPresets();
			}
			return _presets;
		}
	}

	public static ThemePalette Find(string id)
	{
		foreach (ThemePalette preset in Presets)
		{
			if (string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase))
			{
				return preset;
			}
		}
		return Presets[0];
	}

	public static void Apply(string id)
	{
		ThemePalette themePalette = (Current = Find(id));
		ResourceDictionary resources = Application.Current.Resources;
		resources["ThRingBackdrop"] = RadialBackdrop(themePalette.Bg0, themePalette.Bg1);
		resources["ThRingEdge"] = Linear(themePalette.RimA, themePalette.RimB, 0.0, 0.0, 1.0, 1.0);
		resources["ThTrack"] = Solid(themePalette.Track);
		resources["ThShortRing"] = Linear(themePalette.ShortA, themePalette.ShortB, 0.0, 0.0, 1.0, 1.0);
		resources["ThWeekRing"] = Linear(themePalette.WeekA, themePalette.WeekB, 0.0, 0.0, 1.0, 1.0);
		resources["ThWarnRing"] = Linear(themePalette.Warn, Shade(themePalette.Warn, 0.72), 0.0, 0.0, 1.0, 1.0);
		resources["ThDangerRing"] = Linear(themePalette.Danger, Shade(themePalette.Danger, 0.7), 0.0, 0.0, 1.0, 1.0);
		resources["ThHandleFill"] = Linear(themePalette.WeekA, themePalette.WeekB, 0.0, 0.0, 0.0, 1.0);
		resources["ThHandleFillWarn"] = Linear(themePalette.Warn, Shade(themePalette.Warn, 0.72), 0.0, 0.0, 0.0, 1.0);
		resources["ThHandleFillDanger"] = Linear(themePalette.Danger, Shade(themePalette.Danger, 0.7), 0.0, 0.0, 0.0, 1.0);
		resources["ThHandleTrack"] = Solid(themePalette.HandleTrack);
		resources["ThTextHi"] = Solid(themePalette.TextHi);
		resources["ThTextLo"] = Solid(themePalette.TextLo);
		resources["ThTextFaint"] = Solid(themePalette.TextFaint);
		resources["ThAccentShort"] = Solid(themePalette.AccentShort);
		resources["ThAccentWeek"] = Solid(themePalette.AccentWeek);
		resources["ThOk"] = Solid(themePalette.Ok);
		resources["ThWarnText"] = Solid(themePalette.Warn);
		resources["ThDangerText"] = Solid(themePalette.Danger);
		resources["ThSurface"] = Solid(themePalette.Surface);
		resources["ThSurfaceBorder"] = Solid(themePalette.SurfaceBorder);
		resources["ThHoverOverlay"] = Solid(themePalette.Hover);
		resources["ThTipBg"] = Solid(themePalette.TipBg);
		resources["ThTipBorder"] = Solid(themePalette.TipBorder);
		resources["ThMenuBg"] = Solid(themePalette.MenuBg);
		resources["ThMenuBorder"] = Solid(themePalette.MenuBorder);
		resources["ThMenuHover"] = Solid(themePalette.MenuHover);
		resources["ThMenuSeparator"] = Solid(themePalette.MenuSep);
		resources["ThMenuText"] = Solid(themePalette.MenuText);
		resources["ThMenuTextDim"] = Solid(themePalette.MenuDim);
		resources["ThMenuAccent"] = Solid(themePalette.AccentWeek);
		resources["ThGlowColor"] = C(themePalette.Glow);
	}

	public static Color ParseColor(string hex)
	{
		return C(hex);
	}

	public static Color ShadeColor(string hex, double factor)
	{
		return C(Shade(hex, factor));
	}

	private static Color C(string hex)
	{
		return (Color)ColorConverter.ConvertFromString(hex);
	}

	private static SolidColorBrush Solid(string hex)
	{
		SolidColorBrush solidColorBrush = new SolidColorBrush(C(hex));
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private static LinearGradientBrush Linear(string a, string b, double x1, double y1, double x2, double y2)
	{
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush(C(a), C(b), new Point(x1, y1), new Point(x2, y2));
		linearGradientBrush.Freeze();
		return linearGradientBrush;
	}

	private static RadialGradientBrush RadialBackdrop(string center, string edge)
	{
		Color color = C(center);
		Color color2 = C(edge);
		Color color3 = Lerp(color, color2, 0.62);
		RadialGradientBrush radialGradientBrush = new RadialGradientBrush();
		radialGradientBrush.Center = new Point(0.44, 0.38);
		radialGradientBrush.GradientOrigin = new Point(0.38, 0.32);
		radialGradientBrush.RadiusX = 0.8;
		radialGradientBrush.RadiusY = 0.8;
		radialGradientBrush.GradientStops.Add(new GradientStop(color, 0.0));
		radialGradientBrush.GradientStops.Add(new GradientStop(color3, 0.68));
		radialGradientBrush.GradientStops.Add(new GradientStop(color2, 1.0));
		radialGradientBrush.Freeze();
		return radialGradientBrush;
	}

	private static Color Lerp(Color a, Color b, double t)
	{
		return Color.FromArgb((byte)((double)(int)a.A + (double)(b.A - a.A) * t), (byte)((double)(int)a.R + (double)(b.R - a.R) * t), (byte)((double)(int)a.G + (double)(b.G - a.G) * t), (byte)((double)(int)a.B + (double)(b.B - a.B) * t));
	}

	private static string Shade(string hex, double factor)
	{
		Color color = C(hex);
		return Color.FromArgb(color.A, (byte)Math.Min(255.0, (double)(int)color.R * factor), (byte)Math.Min(255.0, (double)(int)color.G * factor), (byte)Math.Min(255.0, (double)(int)color.B * factor)).ToString();
	}

	private static IList<ThemePalette> BuildPresets()
	{
		return new List<ThemePalette>
		{
			new ThemePalette
			{
				Id = "midnight",
				Name = "深空午夜",
				IsLight = false,
				Bg0 = "#F0131C31",
				Bg1 = "#F5060A14",
				RimA = "#A88056FF",
				RimB = "#8C2DD9F5",
				Track = "#1BFFFFFF",
				ShortA = "#FF35DDF4",
				ShortB = "#FF3478FF",
				WeekA = "#FFE65CFA",
				WeekB = "#FF7737FF",
				TextHi = "#F2F6FF",
				TextLo = "#B7C1D6",
				TextFaint = "#8A94AB",
				AccentShort = "#4ED6F2",
				AccentWeek = "#C77DFF",
				Ok = "#5FD9A8",
				Warn = "#F5B65C",
				Danger = "#FF6473",
				Surface = "#F20B111E",
				SurfaceBorder = "#5C4A5974",
				HandleTrack = "#342A3550",
				Hover = "#17FFFFFF",
				TipBg = "#F70A0F1B",
				TipBorder = "#663E4F73",
				MenuBg = "#FA0E1424",
				MenuBorder = "#5C4A5876",
				MenuHover = "#14FFFFFF",
				MenuSep = "#26394B69",
				MenuText = "#E8EEF8",
				MenuDim = "#9AA5BB",
				Glow = "#7B4DE8"
			},
			new ThemePalette
			{
				Id = "obsidian",
				Name = "曜石流金",
				IsLight = false,
				Bg0 = "#F0181307",
				Bg1 = "#F5060402",
				RimA = "#A8F5C86A",
				RimB = "#7A8A6A2F",
				Track = "#1AFFE9C4",
				ShortA = "#FFEDD9A3",
				ShortB = "#FFB98A2E",
				WeekA = "#FFFFD873",
				WeekB = "#FFE0A32E",
				TextHi = "#F8F2E4",
				TextLo = "#C9BFA8",
				TextFaint = "#A39A7E",
				AccentShort = "#E8C87A",
				AccentWeek = "#F5D98F",
				Ok = "#74D6A4",
				Warn = "#F5A44C",
				Danger = "#FF6B5E",
				Surface = "#F20E0B05",
				SurfaceBorder = "#6B5E4A22",
				HandleTrack = "#343A3020",
				Hover = "#14FFE9C4",
				TipBg = "#F70C0A05",
				TipBorder = "#66584826",
				MenuBg = "#FA121006",
				MenuBorder = "#5C5E4E28",
				MenuHover = "#14FFE9C4",
				MenuSep = "#264A3E22",
				MenuText = "#F2ECDC",
				MenuDim = "#A89F84",
				Glow = "#C89B3C"
			},
			new ThemePalette
			{
				Id = "aurora",
				Name = "极光青绿",
				IsLight = false,
				Bg0 = "#F00C2129",
				Bg1 = "#F504090C",
				RimA = "#9C35F4C8",
				RimB = "#8C2AA8F0",
				Track = "#1BE0FFF4",
				ShortA = "#FF3EDCF7",
				ShortB = "#FF2E7CF0",
				WeekA = "#FF52F5B0",
				WeekB = "#FF0FA372",
				TextHi = "#EDFBF5",
				TextLo = "#AFC9C2",
				TextFaint = "#82A099",
				AccentShort = "#55D8F2",
				AccentWeek = "#5CE8B4",
				Ok = "#5FE0A8",
				Warn = "#F5B65C",
				Danger = "#FF6473",
				Surface = "#F207141A",
				SurfaceBorder = "#5C2F5B54",
				HandleTrack = "#34273F3A",
				Hover = "#15E0FFF4",
				TipBg = "#F7061014",
				TipBorder = "#66294F48",
				MenuBg = "#FA0A161B",
				MenuBorder = "#5C2F5B54",
				MenuHover = "#14E0FFF4",
				MenuSep = "#26294F48",
				MenuText = "#E4F5EE",
				MenuDim = "#8FAca4",
				Glow = "#17C08F"
			},
			new ThemePalette
			{
				Id = "mono",
				Name = "石墨极简",
				IsLight = false,
				Bg0 = "#F0202329",
				Bg1 = "#F50A0B0D",
				RimA = "#7A8A9099",
				RimB = "#663A3F46",
				Track = "#1BFFFFFF",
				ShortA = "#FFE6E9EE",
				ShortB = "#FF9AA1AB",
				WeekA = "#FFFFFFFF",
				WeekB = "#FFB9C0CA",
				TextHi = "#F5F7FA",
				TextLo = "#B5BBC4",
				TextFaint = "#7F858E",
				AccentShort = "#C9CFD8",
				AccentWeek = "#F0F3F7",
				Ok = "#8FD9B4",
				Warn = "#E8C27C",
				Danger = "#F58A8A",
				Surface = "#F2131519",
				SurfaceBorder = "#5C3F444C",
				HandleTrack = "#34303439",
				Hover = "#14FFFFFF",
				TipBg = "#F7101215",
				TipBorder = "#663A3F46",
				MenuBg = "#FA16181C",
				MenuBorder = "#5C41464E",
				MenuHover = "#12FFFFFF",
				MenuSep = "#263A3F46",
				MenuText = "#ECEFF3",
				MenuDim = "#9BA1AA",
				Glow = "#767C85"
			},
			new ThemePalette
			{
				Id = "sakura",
				Name = "樱粉映雪",
				IsLight = true,
				Bg0 = "#F8FFF5F8",
				Bg1 = "#F8F0D8E4",
				RimA = "#C4E06090",
				RimB = "#A8B050C8",
				Track = "#22182028",
				ShortA = "#FF9A4AD9",
				ShortB = "#FF6B2EC0",
				WeekA = "#FFE0458A",
				WeekB = "#FFB82060",
				TextHi = "#241018",
				TextLo = "#5A2838",
				TextFaint = "#7A4050",
				AccentShort = "#7A2EB0",
				AccentWeek = "#B01858",
				Ok = "#1E8A5C",
				Warn = "#B06A14",
				Danger = "#C02838",
				Surface = "#F8FFF6F9",
				SurfaceBorder = "#A0C06088",
				HandleTrack = "#30182028",
				Hover = "#14201018",
				TipBg = "#FAFFF8FA",
				TipBorder = "#A0C06088",
				MenuBg = "#FCFFFBFC",
				MenuBorder = "#90C06088",
				MenuHover = "#14201018",
				MenuSep = "#40C06088",
				MenuText = "#241018",
				MenuDim = "#6A3848",
				Glow = "#D07098"
			}
		};
	}
}
