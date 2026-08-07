using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CodexQuota.Services;

public static class AutoStart
{
	private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string ValueName = "CodexOrbit";

	public static bool IsEnabled()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: false);
			if (registryKey == null)
			{
				return false;
			}
			string text = registryKey.GetValue("CodexOrbit") as string;
			if (string.IsNullOrWhiteSpace(text))
			{
				return false;
			}
			string b = ExecutablePathQuoted();
			return string.Equals(text.Trim(), b, StringComparison.OrdinalIgnoreCase) || string.Equals(text.Trim('"', ' '), ExecutablePath(), StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	public static bool SetEnabled(bool enabled)
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true) ?? Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
			if (registryKey == null)
			{
				return false;
			}
			if (enabled)
			{
				registryKey.SetValue("CodexOrbit", ExecutablePathQuoted());
			}
			else if (registryKey.GetValue("CodexOrbit") != null)
			{
				registryKey.DeleteValue("CodexOrbit", throwOnMissingValue: false);
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static string ExecutablePath()
	{
		return Application.ExecutablePath;
	}

	private static string ExecutablePathQuoted()
	{
		return "\"" + ExecutablePath() + "\"";
	}
}
