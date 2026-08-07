using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CodexQuota.Services;

namespace CodexQuota;

public partial class App : Application
{
	private const string RevealSignalName = "CodexOrbit.Wpf.Reveal";

	private const int MaximumDiagnosticLength = 32768;

	private Mutex _singleInstance;

	private bool _ownsMutex;

	private EventWaitHandle _revealSignal;

	private Thread _revealThread;

	public static AppSettings Settings { get; private set; }

	protected override void OnStartup(StartupEventArgs e)
	{
		string text = null;
		string text2 = null;
		string text3 = null;
		if (e.Args != null)
		{
			for (int i = 0; i < e.Args.Length - 1; i++)
			{
				if (string.Equals(e.Args[i], "--render-preview", StringComparison.OrdinalIgnoreCase))
				{
					text = e.Args[i + 1];
				}
				else if (string.Equals(e.Args[i], "--render-mini-preview", StringComparison.OrdinalIgnoreCase))
				{
					text2 = e.Args[i + 1];
				}
				else if (string.Equals(e.Args[i], "--theme", StringComparison.OrdinalIgnoreCase))
				{
					text3 = e.Args[i + 1];
				}
			}
		}
		bool flag = text != null || text2 != null;
		string name = (flag ? ("CodexOrbit.Wpf.Preview." + Process.GetCurrentProcess().Id) : "CodexOrbit.Wpf.SingleInstance");
		_singleInstance = new Mutex(initiallyOwned: true, name, out var createdNew);
		_ownsMutex = createdNew;
		if (!createdNew)
		{
			try
			{
				if (EventWaitHandle.TryOpenExisting("CodexOrbit.Wpf.Reveal", out var result))
				{
					using (result)
					{
						result.Set();
					}
				}
			}
			catch
			{
			}
			Shutdown();
			return;
		}
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WINDIR")))
		{
			string environmentVariable = Environment.GetEnvironmentVariable("SystemRoot");
			if (!string.IsNullOrWhiteSpace(environmentVariable))
			{
				Environment.SetEnvironmentVariable("WINDIR", environmentVariable, EnvironmentVariableTarget.Process);
			}
		}
		base.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs args)
		{
			LogError(args.Exception);
			args.Handled = false;
		};
		AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs args)
		{
			LogError(args.ExceptionObject as Exception);
		};
		base.OnStartup(e);
		Settings = AppSettings.Load();
		ThemeManager.Apply(text3 ?? Settings.Theme);
		if (!flag && !string.Equals(Settings.Theme, ThemeManager.Current.Id, StringComparison.OrdinalIgnoreCase))
		{
			Settings.Theme = ThemeManager.Current.Id;
			Settings.Save();
		}
		MainWindow window = new MainWindow(text, text2);
		base.MainWindow = window;
		if (!flag)
		{
			_revealSignal = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, "CodexOrbit.Wpf.Reveal");
			_revealThread = new Thread((ThreadStart)delegate
			{
				while (true)
				{
					try
					{
						if (!_revealSignal.WaitOne())
						{
							break;
						}
					}
					catch (ObjectDisposedException)
					{
						break;
					}
					catch (Exception)
					{
						break;
					}
					try
					{
						base.Dispatcher.BeginInvoke(new Action(window.RevealFromExternal));
					}
					catch
					{
						break;
					}
				}
			})
			{
				IsBackground = true,
				Name = "CodexOrbit.RevealListener"
			};
			_revealThread.Start();
		}
		window.Show();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (_revealSignal != null)
		{
			try
			{
				_revealSignal.Dispose();
			}
			catch
			{
			}
		}
		if (_singleInstance != null)
		{
			if (_ownsMutex)
			{
				_singleInstance.ReleaseMutex();
			}
			_singleInstance.Dispose();
		}
		base.OnExit(e);
	}

	internal static void LogError(Exception exception)
	{
		if (exception == null)
		{
			return;
		}
		try
		{
			Directory.CreateDirectory(AppSettings.SettingsDirectory);
			string text = Path.Combine(AppSettings.SettingsDirectory, "error.log");
			FileInfo fileInfo = new FileInfo(text);
			if (fileInfo.Exists && fileInfo.Length > 524288)
			{
				try
				{
					fileInfo.Delete();
				}
				catch
				{
				}
			}
			File.AppendAllText(text, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + SanitizeDiagnostic(exception.ToString()) + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		}
		catch
		{
		}
	}

	internal static string SanitizeDiagnostic(string diagnostic)
	{
		string value = diagnostic ?? "";
		value = ReplacePath(value, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%");
		value = ReplacePath(value, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "%USERPROFILE%");
		value = Regex.Replace(value, @"github_pat_[A-Za-z0-9_]{16,}", "[REDACTED_GITHUB_TOKEN]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		value = Regex.Replace(value, @"gh[pousr]_[A-Za-z0-9_]{16,}", "[REDACTED_GITHUB_TOKEN]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		value = Regex.Replace(value, @"sk-[A-Za-z0-9_-]{16,}", "[REDACTED_API_KEY]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		if (value.Length > MaximumDiagnosticLength)
		{
			value = value.Substring(0, MaximumDiagnosticLength) + Environment.NewLine + "[DIAGNOSTIC_TRUNCATED]";
		}
		return value;
	}

	private static string ReplacePath(string value, string path, string replacement)
	{
		if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(path))
		{
			return value;
		}
		int index = value.IndexOf(path, StringComparison.OrdinalIgnoreCase);
		while (index >= 0)
		{
			value = value.Substring(0, index) + replacement + value.Substring(index + path.Length);
			index = value.IndexOf(path, index + replacement.Length, StringComparison.OrdinalIgnoreCase);
		}
		return value;
	}
}
