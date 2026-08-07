using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace CodexQuota.Services;

public sealed class AppSettings
{
	public const string ModeMini = "Mini";

	public const string ModeRing = "Ring";

	public const string ModeBoth = "Both";

	public string Theme = "midnight";

	public string DisplayMode = "Mini";

	public int OpacityPercent = 100;

	public bool ClickThrough;

	public bool Topmost = true;

	public bool HideWhenFullscreen = true;

	public int LowQuotaNotifyPercent = 10;

	public bool NotifyOnReset = true;

	public long TrackedWeekResetsAtTicks;

	public long NotifiedWeekDeadlineTicks;

	public string TrayIconStyle = "percent";

	public string PlanOverride = "auto";

	public double RingLeft = double.NaN;

	public double RingTop = double.NaN;

	public double RingSize = 120.0;

	public double MiniLeft = double.NaN;

	public double MiniTop = double.NaN;

	public string MiniDock = "None";

	private string _lastSavedJson;

	public static string SettingsDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexQuota");

	private static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

	public static AppSettings Load()
	{
		AppSettings appSettings = new AppSettings();
		try
		{
			if (File.Exists(SettingsPath))
			{
				JavaScriptSerializer javaScriptSerializer = new JavaScriptSerializer();
				string text = File.ReadAllText(SettingsPath, Encoding.UTF8);
				if (javaScriptSerializer.DeserializeObject(text) is IDictionary<string, object> map)
				{
					appSettings.Theme = ReadString(map, "theme", appSettings.Theme);
					appSettings.DisplayMode = NormalizeMode(ReadString(map, "displayMode", appSettings.DisplayMode));
					appSettings.OpacityPercent = ClampOpacity(ReadInt(map, "opacityPercent", appSettings.OpacityPercent));
					appSettings.ClickThrough = ReadBool(map, "clickThrough", appSettings.ClickThrough);
					appSettings.Topmost = ReadBool(map, "topmost", appSettings.Topmost);
					appSettings.HideWhenFullscreen = ReadBool(map, "hideWhenFullscreen", appSettings.HideWhenFullscreen);
					appSettings.LowQuotaNotifyPercent = ReadInt(map, "lowQuotaNotifyPercent", appSettings.LowQuotaNotifyPercent);
					appSettings.NotifyOnReset = ReadBool(map, "notifyOnReset", appSettings.NotifyOnReset);
					appSettings.TrackedWeekResetsAtTicks = ReadLong(map, "trackedWeekResetsAtTicks", appSettings.TrackedWeekResetsAtTicks);
					appSettings.NotifiedWeekDeadlineTicks = ReadLong(map, "notifiedWeekDeadlineTicks", appSettings.NotifiedWeekDeadlineTicks);
					appSettings.TrayIconStyle = ReadString(map, "trayIconStyle", appSettings.TrayIconStyle);
					appSettings.PlanOverride = ReadString(map, "planOverride", appSettings.PlanOverride);
					appSettings.RingLeft = ReadDouble(map, "ringLeft", appSettings.RingLeft);
					appSettings.RingTop = ReadDouble(map, "ringTop", appSettings.RingTop);
					appSettings.RingSize = ReadDouble(map, "ringSize", appSettings.RingSize);
					appSettings.MiniLeft = ReadDouble(map, "miniLeft", appSettings.MiniLeft);
					appSettings.MiniTop = ReadDouble(map, "miniTop", appSettings.MiniTop);
					appSettings.MiniDock = ReadString(map, "miniDock", appSettings.MiniDock);
					appSettings._lastSavedJson = text;
					Normalize(appSettings);
					return appSettings;
				}
			}
		}
		catch
		{
		}
		MigrateLegacy(appSettings);
		Normalize(appSettings);
		return appSettings;
	}

	public void Save()
	{
		try
		{
			Normalize(this);
			Directory.CreateDirectory(SettingsDirectory);
			Dictionary<string, object> obj = new Dictionary<string, object>
			{
				{ "version", 3 },
				{ "theme", Theme },
				{ "displayMode", DisplayMode },
				{ "opacityPercent", OpacityPercent },
				{ "clickThrough", ClickThrough },
				{ "topmost", Topmost },
				{ "hideWhenFullscreen", HideWhenFullscreen },
				{ "lowQuotaNotifyPercent", LowQuotaNotifyPercent },
				{ "notifyOnReset", NotifyOnReset },
				{ "trackedWeekResetsAtTicks", TrackedWeekResetsAtTicks },
				{ "notifiedWeekDeadlineTicks", NotifiedWeekDeadlineTicks },
				{ "trayIconStyle", TrayIconStyle },
				{
					"planOverride",
					string.IsNullOrWhiteSpace(PlanOverride) ? "auto" : PlanOverride
				},
				{
					"ringLeft",
					Sanitize(RingLeft)
				},
				{
					"ringTop",
					Sanitize(RingTop)
				},
				{
					"ringSize",
					Sanitize(RingSize)
				},
				{
					"miniLeft",
					Sanitize(MiniLeft)
				},
				{
					"miniTop",
					Sanitize(MiniTop)
				},
				{ "miniDock", MiniDock }
			};
			string text = new JavaScriptSerializer().Serialize(obj);
			if (string.Equals(text, _lastSavedJson, StringComparison.Ordinal))
			{
				return;
			}
			string text2 = SettingsPath + ".tmp";
			File.WriteAllText(text2, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			if (File.Exists(SettingsPath))
			{
				try
				{
					File.Replace(text2, SettingsPath, null, ignoreMetadataErrors: true);
				}
				catch (PlatformNotSupportedException)
				{
					File.Copy(text2, SettingsPath, overwrite: true);
					File.Delete(text2);
				}
			}
			else
			{
				File.Move(text2, SettingsPath);
			}
			_lastSavedJson = text;
		}
		catch
		{
		}
	}

	private static void Normalize(AppSettings settings)
	{
		settings.Theme = NormalizeTheme(settings.Theme);
		settings.DisplayMode = NormalizeMode(settings.DisplayMode);
		settings.OpacityPercent = ClampOpacity(settings.OpacityPercent);
		settings.LowQuotaNotifyPercent = NormalizeNotifyThreshold(settings.LowQuotaNotifyPercent);
		settings.TrayIconStyle = NormalizeTrayStyle(settings.TrayIconStyle);
		settings.PlanOverride = NormalizePlanOverride(settings.PlanOverride);
		settings.RingLeft = NormalizeCoordinate(settings.RingLeft);
		settings.RingTop = NormalizeCoordinate(settings.RingTop);
		settings.RingSize = double.IsNaN(settings.RingSize) || double.IsInfinity(settings.RingSize) ? 120.0 : Math.Max(90.0, Math.Min(480.0, settings.RingSize));
		settings.MiniLeft = NormalizeCoordinate(settings.MiniLeft);
		settings.MiniTop = NormalizeCoordinate(settings.MiniTop);
		settings.MiniDock = NormalizeDock(settings.MiniDock);
	}

	private static string NormalizeTheme(string theme)
	{
		switch ((theme ?? "").Trim().ToLowerInvariant())
		{
		case "midnight":
		case "obsidian":
		case "aurora":
		case "mono":
		case "sakura":
			return theme.Trim().ToLowerInvariant();
		default:
			return "midnight";
		}
	}

	private static int NormalizeNotifyThreshold(int value)
	{
		switch (value)
		{
		case 0:
		case 5:
		case 10:
		case 20:
			return value;
		default:
			return 10;
		}
	}

	private static string NormalizeTrayStyle(string style)
	{
		if (string.Equals(style, "ring", StringComparison.OrdinalIgnoreCase))
		{
			return "ring";
		}
		if (string.Equals(style, "logo", StringComparison.OrdinalIgnoreCase))
		{
			return "logo";
		}
		return "percent";
	}

	private static string NormalizePlanOverride(string value)
	{
		switch ((value ?? "").Trim().ToLowerInvariant())
		{
		case "free":
		case "go":
		case "plus":
		case "pro5x":
		case "pro20x":
		case "business":
		case "enterprise":
			return value.Trim().ToLowerInvariant();
		default:
			return "auto";
		}
	}

	private static double NormalizeCoordinate(double value)
	{
		return double.IsNaN(value) || double.IsInfinity(value) ? double.NaN : value;
	}

	private static string NormalizeDock(string value)
	{
		if (string.Equals(value, "Left", StringComparison.OrdinalIgnoreCase))
		{
			return "Left";
		}
		if (string.Equals(value, "Right", StringComparison.OrdinalIgnoreCase))
		{
			return "Right";
		}
		return "None";
	}

	private static void MigrateLegacy(AppSettings settings)
	{
		try
		{
			string path = Path.Combine(SettingsDirectory, "window-ring-v2.txt");
			if (File.Exists(path))
			{
				string[] array = File.ReadAllLines(path);
				if (array.Length >= 2 && double.TryParse(array[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result) && double.TryParse(array[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
				{
					settings.RingLeft = result;
					settings.RingTop = result2;
					bool result5;
					if (array.Length >= 5 && double.TryParse(array[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var result3) && double.TryParse(array[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var result4))
					{
						settings.RingSize = Math.Max(result3, result4);
						if (bool.TryParse(array[4], out result5))
						{
							settings.Topmost = result5;
						}
					}
					else if (array.Length >= 3 && bool.TryParse(array[2], out result5))
					{
						settings.Topmost = result5;
					}
				}
			}
			string path2 = Path.Combine(SettingsDirectory, "window-mini-v1.txt");
			if (!File.Exists(path2))
			{
				return;
			}
			string[] array2 = File.ReadAllLines(path2);
			if (array2.Length >= 2 && double.TryParse(array2[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var result6) && double.TryParse(array2[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var result7))
			{
				settings.MiniLeft = result6;
				settings.MiniTop = result7;
				if (array2.Length >= 3 && !string.IsNullOrWhiteSpace(array2[2]))
				{
					settings.MiniDock = array2[2].Trim();
				}
			}
		}
		catch
		{
		}
	}

	private static string NormalizeMode(string mode)
	{
		if (string.Equals(mode, "Ring", StringComparison.OrdinalIgnoreCase))
		{
			return "Ring";
		}
		if (string.Equals(mode, "Both", StringComparison.OrdinalIgnoreCase))
		{
			return "Both";
		}
		return "Mini";
	}

	private static int ClampOpacity(int value)
	{
		if (value < 40)
		{
			return 40;
		}
		if (value > 100)
		{
			return 100;
		}
		return value;
	}

	private static object Sanitize(double value)
	{
		if (!double.IsNaN(value) && !double.IsInfinity(value))
		{
			return value;
		}
		return null;
	}

	private static string ReadString(IDictionary<string, object> map, string key, string fallback)
	{
		if (!map.TryGetValue(key, out var value) || !(value is string) || string.IsNullOrWhiteSpace((string)value))
		{
			return fallback;
		}
		return (string)value;
	}

	private static bool ReadBool(IDictionary<string, object> map, string key, bool fallback)
	{
		if (!map.TryGetValue(key, out var value) || !(value is bool))
		{
			return fallback;
		}
		return (bool)value;
	}

	private static int ReadInt(IDictionary<string, object> map, string key, int fallback)
	{
		if (!map.TryGetValue(key, out var value) || value == null)
		{
			return fallback;
		}
		try
		{
			return Convert.ToInt32(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return fallback;
		}
	}

	private static long ReadLong(IDictionary<string, object> map, string key, long fallback)
	{
		if (!map.TryGetValue(key, out var value) || value == null)
		{
			return fallback;
		}
		try
		{
			return Convert.ToInt64(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return fallback;
		}
	}

	private static double ReadDouble(IDictionary<string, object> map, string key, double fallback)
	{
		if (!map.TryGetValue(key, out var value) || value == null)
		{
			return fallback;
		}
		try
		{
			return Convert.ToDouble(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return fallback;
		}
	}
}
