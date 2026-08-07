using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using CodexQuota.Models;
using CodexQuota.Services;

namespace CodexQuota;

public partial class MainWindow : Window, IComponentConnector
{
	private readonly AppSettings _settings;

	private readonly CodexUsageReader _reader;

	private readonly DispatcherTimer _clockTimer;

	private readonly DispatcherTimer _resizeSaveTimer;

	private readonly DispatcherTimer _detailTipTimer;

	private readonly NotifyIcon _trayIcon;

	private readonly MiniStatusWindow _miniStatusWindow;

	private readonly string _previewPath;

	private readonly string _miniPreviewPath;

	private readonly bool _isPreview;

	private System.Windows.Controls.ContextMenu _menu;

	private readonly Dictionary<string, System.Windows.Controls.MenuItem> _modeItems = new Dictionary<string, System.Windows.Controls.MenuItem>();

	private readonly Dictionary<string, System.Windows.Controls.MenuItem> _themeItems = new Dictionary<string, System.Windows.Controls.MenuItem>();

	private readonly Dictionary<int, System.Windows.Controls.MenuItem> _opacityItems = new Dictionary<int, System.Windows.Controls.MenuItem>();

	private readonly Dictionary<string, System.Windows.Controls.MenuItem> _trayStyleItems = new Dictionary<string, System.Windows.Controls.MenuItem>();

	private readonly Dictionary<string, System.Windows.Controls.MenuItem> _planItems = new Dictionary<string, System.Windows.Controls.MenuItem>();

	private readonly Dictionary<int, System.Windows.Controls.MenuItem> _notifyItems = new Dictionary<int, System.Windows.Controls.MenuItem>();

	private System.Windows.Controls.MenuItem _resetNotifyItem;

	private System.Windows.Controls.MenuItem _topmostItem;

	private System.Windows.Controls.MenuItem _hideFullscreenItem;

	private System.Windows.Controls.MenuItem _clickThroughItem;

	private System.Windows.Controls.MenuItem _autoStartItem;

	private UsageSnapshot _snapshot;

	private bool _allowClose;

	private bool _isExiting;

	private HwndSource _windowSource;

	private bool _maintainingAspectRatio;

	private bool _suppressedByFullscreen;

	private double _appliedShortPct = -1.0;

	private double _appliedWeekPct = -1.0;

	private bool _appliedShortVisible = true;

	private double _appliedLayoutSize = -1.0;

	private string _shortRingKey = "ThShortRing";

	private string _weekRingKey = "ThWeekRing";

	private string _shortTextKey = "ThAccentShort";

	private string _weekTextKey = "ThAccentWeek";

	private string _syncStatusKey = "ThOk";

	private string _lastTrayText;

	private string _lastTrayIconKey;

	private long _warnedCycleShort;

	private long _warnedCycleWeek;

	private const int WmNcHitTest = 132;

	private const int HtLeft = 10;

	private const int HtRight = 11;

	private const int HtTop = 12;

	private const int HtTopLeft = 13;

	private const int HtTopRight = 14;

	private const int HtBottom = 15;

	private const int HtBottomLeft = 16;

	private const int HtBottomRight = 17;

	private const int HtTransparent = -1;

	public MainWindow(string previewPath, string miniPreviewPath)
	{
		InitializeComponent();
		_previewPath = previewPath;
		_miniPreviewPath = miniPreviewPath;
		_isPreview = !string.IsNullOrWhiteSpace(previewPath) || !string.IsNullOrWhiteSpace(miniPreviewPath);
		_settings = App.Settings ?? new AppSettings();
		_reader = new CodexUsageReader(CodexUsageReader.GetDefaultSessionsPath());
		_reader.SnapshotChanged += Reader_SnapshotChanged;
		_clockTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(5.0)
		};
		_clockTimer.Tick += delegate
		{
			UpdateFullscreenSuppression();
			RefreshUi(force: false);
		};
		_resizeSaveTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(700.0)
		};
		_resizeSaveTimer.Tick += delegate
		{
			_resizeSaveTimer.Stop();
			SaveRingBounds();
		};
		_detailTipTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(3.0)
		};
		_detailTipTimer.Tick += delegate
		{
			CloseDetailToolTip();
		};
		_miniStatusWindow = new MiniStatusWindow(_settings, delegate
		{
			base.Dispatcher.BeginInvoke(new Action(ShowContextMenu));
		});
		BuildContextMenu();
		_trayIcon = new NotifyIcon
		{
			Text = "Codex Orbit · 等待额度数据",
			Icon = TrayIconRenderer.LoadLogoIcon(),
			Visible = true
		};
		_trayIcon.MouseUp += TrayIcon_MouseUp;
		_trayIcon.DoubleClick += delegate
		{
			base.Dispatcher.BeginInvoke(new Action(RevealFromExternal));
		};
		_trayIcon.BalloonTipClicked += delegate
		{
			base.Dispatcher.BeginInvoke(new Action(RevealFromExternal));
		};
		base.Opacity = (double)_settings.OpacityPercent / 100.0;
	}

	private async void Window_Loaded(object sender, RoutedEventArgs e)
	{
		RestoreWindowSettings();
		if (!_isPreview)
		{
			ApplyDisplayMode(initialBoot: true);
			_clockTimer.Start();
			_reader.StartWatching();
			UsageSnapshot initialSnapshot = await Task.Run((Func<UsageSnapshot>)_reader.ReadLatest);
			if (!_isExiting)
			{
				ApplySnapshot(initialSnapshot);
			}
			return;
		}
		ApplySnapshot(CreateDemoSnapshot());
		if (!string.IsNullOrWhiteSpace(_miniPreviewPath))
		{
			GetCurrentScreenRects(out var workingArea, out var screenBounds);
			Hide();
			_miniStatusWindow.ShowNearTaskbar(workingArea, screenBounds);
			await Task.Delay(260);
			_miniStatusWindow.RenderPreview(_miniPreviewPath);
			ExitApplication();
		}
		else
		{
			Show();
			LayoutGauge();
			StartEntranceAnimation();
			await Task.Delay(450);
			RenderPreview(_previewPath);
			ExitApplication();
		}
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		_windowSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
		if (_windowSource != null)
		{
			_windowSource.AddHook(WindowMessageHook);
			NativeMethods.ApplyWidgetStyles(_windowSource.Handle, _settings.ClickThrough);
		}
	}

	private void Window_Closing(object sender, CancelEventArgs e)
	{
		if (!_allowClose)
		{
			e.Cancel = true;
			Hide();
			if (!_miniStatusWindow.IsVisible)
			{
				ShowMiniWindow();
			}
		}
	}

	private void ExitApplication()
	{
		if (_isExiting)
		{
			return;
		}
		_isExiting = true;
		_allowClose = true;
		if (!_isPreview)
		{
			SaveRingBounds();
			_settings.Save();
		}
		_clockTimer.Stop();
		_resizeSaveTimer.Stop();
		CloseDetailToolTip();
		_reader.Dispose();
		if (_windowSource != null)
		{
			_windowSource.RemoveHook(WindowMessageHook);
		}
		_miniStatusWindow.Close();
		_trayIcon.Visible = false;
		if (_trayIcon.Icon != null)
		{
			_trayIcon.Icon.Dispose();
		}
		_trayIcon.Dispose();
		Close();
		System.Windows.Application.Current.Shutdown();
	}

	private void Reader_SnapshotChanged(object sender, UsageSnapshot snapshot)
	{
		if (_isExiting)
		{
			return;
		}
		try
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				if (!_isExiting)
				{
					ApplySnapshot(snapshot);
				}
			});
		}
		catch (InvalidOperationException)
		{
		}
	}

	private void ApplySnapshot(UsageSnapshot snapshot)
	{
		_snapshot = snapshot ?? new UsageSnapshot
		{
			StatusMessage = "暂无数据"
		};
		CheckNotifications();
		RefreshUi(force: false);
	}

	private void RefreshUi(bool force)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		WindowDisplay shortDisplay = WindowDisplay.FromShort((_snapshot == null) ? null : _snapshot.ShortWindow, now);
		WindowDisplay weekDisplay = WindowDisplay.From((_snapshot == null) ? null : _snapshot.WeekWindow, now);
		UpdateRing(shortDisplay, weekDisplay, now, force);
		UpdateToolTip(shortDisplay, weekDisplay, now);
		_miniStatusWindow.ApplyState(BuildMiniState(shortDisplay, weekDisplay, now), force);
		UpdateTray(shortDisplay, weekDisplay, force);
	}

	private void UpdateRing(WindowDisplay shortDisplay, WindowDisplay weekDisplay, DateTimeOffset now, bool force)
	{
		SetText(WeekPercent, weekDisplay.Exists ? (weekDisplay.Label + " " + weekDisplay.RoundedPercent + "%") : "7d --");
		SetText(ShortPercent, shortDisplay.Exists ? (shortDisplay.Label + " " + shortDisplay.RoundedPercent + "%") : "5h --");
		WeekPercent.Opacity = (weekDisplay.Exists ? 1.0 : 0.52);
		bool exists = shortDisplay.Exists;
		if ((exists != _appliedShortVisible) | force)
		{
			_appliedShortVisible = exists;
			ShortPercent.Visibility = ((!exists) ? Visibility.Collapsed : Visibility.Visible);
			ShortRing.Visibility = ((!exists) ? Visibility.Collapsed : Visibility.Visible);
			ShortTrack.Visibility = ((!exists) ? Visibility.Collapsed : Visibility.Visible);
			_appliedLayoutSize = -1.0;
		}
		ApplyBrushKey(WeekRing, Shape.StrokeProperty, RingBrushKey(weekDisplay.Alert, isWeek: true), ref _weekRingKey);
		ApplyBrushKey(ShortRing, Shape.StrokeProperty, RingBrushKey(shortDisplay.Alert, isWeek: false), ref _shortRingKey);
		ApplyBrushKey(WeekPercent, TextBlock.ForegroundProperty, TextBrushKey(weekDisplay.Alert, isWeek: true), ref _weekTextKey);
		ApplyBrushKey(ShortPercent, TextBlock.ForegroundProperty, TextBrushKey(shortDisplay.Alert, isWeek: false), ref _shortTextKey);
		SetText(ResetText, BuildResetLine(shortDisplay, weekDisplay, now));
		double num = (shortDisplay.Exists ? shortDisplay.Percent : 0.0);
		double num2 = (weekDisplay.Exists ? weekDisplay.Percent : 0.0);
		if (force || Math.Abs(num - _appliedShortPct) > 0.05 || Math.Abs(num2 - _appliedWeekPct) > 0.05)
		{
			_appliedShortPct = num;
			_appliedWeekPct = num2;
			_appliedLayoutSize = -1.0;
		}
		LayoutGauge();
	}

	private static string BuildResetLine(WindowDisplay shortDisplay, WindowDisplay weekDisplay, DateTimeOffset now)
	{
		WindowDisplay windowDisplay = null;
		if (shortDisplay.Exists && shortDisplay.Stage == QuotaStage.Fresh)
		{
			windowDisplay = shortDisplay;
		}
		if (weekDisplay.Exists && weekDisplay.Stage == QuotaStage.Fresh && (windowDisplay == null || weekDisplay.Source.ResetsAt < windowDisplay.Source.ResetsAt))
		{
			windowDisplay = weekDisplay;
		}
		if (windowDisplay != null)
		{
			if (windowDisplay.IsUnused)
			{
				return "新周期·待使用";
			}
			return UsageFormatter.CountdownCompact(windowDisplay.Source.ResetsAt - now) + " 后重置";
		}
		if (shortDisplay.Exists || weekDisplay.Exists)
		{
			return "快照过期·待同步";
		}
		return "等待数据";
	}

	private void UpdateToolTip(WindowDisplay shortDisplay, WindowDisplay weekDisplay, DateTimeOffset now)
	{
		bool flag = shortDisplay.Exists || weekDisplay.Exists;
		bool num = (shortDisplay.Exists && shortDisplay.Stage == QuotaStage.Fresh) || (weekDisplay.Exists && weekDisplay.Stage == QuotaStage.Fresh);
		bool flag2 = (shortDisplay.Exists && shortDisplay.IsUnused) || (weekDisplay.Exists && weekDisplay.IsUnused);
		bool flag3 = (shortDisplay.Exists && shortDisplay.Stage == QuotaStage.Stale) || (weekDisplay.Exists && weekDisplay.Stage == QuotaStage.Stale);
		string value;
		string key;
		if (_snapshot != null && !_snapshot.IsLive)
		{
			value = (string.IsNullOrWhiteSpace(_snapshot.StatusMessage) ? "非实时 · 等待账户服务" : _snapshot.StatusMessage);
			key = "ThWarnText";
		}
		else if (num)
		{
			if (flag2)
			{
				value = "实时同步 · 新周期";
				key = "ThOk";
			}
			else if (flag3)
			{
				value = "部分快照已过期";
				key = "ThWarnText";
			}
			else
			{
				value = ((_snapshot == null || string.IsNullOrWhiteSpace(_snapshot.StatusMessage)) ? "实时同步" : _snapshot.StatusMessage);
				key = "ThOk";
			}
		}
		else if (flag)
		{
			value = "快照已过期 · 使用后同步";
			key = "ThWarnText";
		}
		else
		{
			value = ((_snapshot == null || string.IsNullOrWhiteSpace(_snapshot.StatusMessage)) ? "暂无数据" : _snapshot.StatusMessage);
			key = "ThWarnText";
		}
		SetText(SyncStatusText, value);
		ApplyBrushKey(SyncStatusText, TextBlock.ForegroundProperty, key, ref _syncStatusKey);
		SetText(ShortDetailLabel, (shortDisplay.Exists && shortDisplay.Label != "5h") ? shortDisplay.Label : "5 小时");
		SetText(WeekDetailLabel, (weekDisplay.Exists && weekDisplay.Label != "7d") ? weekDisplay.Label : "周额度");
		SetText(ShortDetailText, UsageFormatter.WindowDetail(shortDisplay, now));
		SetText(WeekDetailText, UsageFormatter.WindowDetail(weekDisplay, now));
		ShortDetailRow.Visibility = ((!shortDisplay.Exists) ? Visibility.Collapsed : Visibility.Visible);
		PlanBadge planBadge = PlanInfo.Resolve((_snapshot == null) ? null : _snapshot.PlanType, _settings.PlanOverride);
		SetText(PlanText, planBadge.ShowBadge ? planBadge.Title : "");
		DateTimeOffset? dateTimeOffset = null;
		if (shortDisplay.Exists)
		{
			dateTimeOffset = shortDisplay.Source.ObservedAt;
		}
		if (weekDisplay.Exists && (!dateTimeOffset.HasValue || weekDisplay.Source.ObservedAt > dateTimeOffset.Value))
		{
			dateTimeOffset = weekDisplay.Source.ObservedAt;
		}
		SetText(LastSyncText, dateTimeOffset.HasValue ? UsageFormatter.SnapshotTime(dateTimeOffset.Value, now) : "--");
	}

	private MiniViewState BuildMiniState(WindowDisplay shortDisplay, WindowDisplay weekDisplay, DateTimeOffset now)
	{
		MiniViewState miniViewState = new MiniViewState();
		PlanBadge planBadge = PlanInfo.Resolve((_snapshot == null) ? null : _snapshot.PlanType, _settings.PlanOverride);
		miniViewState.ShowPlanBadge = planBadge.ShowBadge;
		miniViewState.PlanKind = planBadge.Kind;
		miniViewState.PlanTitle = planBadge.Title;
		miniViewState.PlanSubtitle = planBadge.Subtitle;
		miniViewState.PlanGlyph = planBadge.Glyph;
		miniViewState.ShowPill = weekDisplay.Exists;
		if (weekDisplay.Exists)
		{
			miniViewState.PillLabel = "重置";
			miniViewState.PillAlert = weekDisplay.Alert;
			if (weekDisplay.Stage == QuotaStage.Stale)
			{
				miniViewState.PillValue = "待同步";
				miniViewState.PillReset = "主额度快照已过期";
			}
			else
			{
				miniViewState.PillValue = UsageFormatter.CountdownRemainingCompact(weekDisplay.Source.ResetsAt - now);
				miniViewState.PillReset = "主额度剩余 " + weekDisplay.RoundedPercent + "%";
			}
		}
		else
		{
			miniViewState.PillLabel = "5h";
			miniViewState.PillValue = "--";
			miniViewState.PillAlert = AlertLevel.Unknown;
			miniViewState.PillReset = "等待同步";
		}
		WindowDisplay windowDisplay = (weekDisplay.Exists ? weekDisplay : shortDisplay);
		miniViewState.GaugeValid = windowDisplay.Exists;
		miniViewState.GaugeIsWeek = weekDisplay.Exists;
		miniViewState.GaugePercent = (windowDisplay.Exists ? windowDisplay.Percent : 0.0);
		miniViewState.GaugeValue = (windowDisplay.Exists ? (windowDisplay.RoundedPercent + "%") : "--");
		miniViewState.GaugeAlert = windowDisplay.Alert;
		miniViewState.GaugeLabel = (windowDisplay.Exists ? ((windowDisplay.Stage == QuotaStage.Stale) ? "主额度·旧" : "主额度") : "同步");
		miniViewState.GaugeReset = ((!windowDisplay.Exists) ? "等待同步" : ((windowDisplay.Stage == QuotaStage.Stale) ? "快照已过期" : windowDisplay.Label + " 周期"));
		bool num = (shortDisplay.Exists && shortDisplay.Stage == QuotaStage.Fresh) || (weekDisplay.Exists && weekDisplay.Stage == QuotaStage.Fresh);
		bool flag = (shortDisplay.Exists && shortDisplay.IsUnused) || (weekDisplay.Exists && weekDisplay.IsUnused);
		if (_snapshot != null && !_snapshot.IsLive)
		{
			miniViewState.TipStatus = (string.IsNullOrWhiteSpace(_snapshot.StatusMessage) ? "非实时 · 等待账户服务" : _snapshot.StatusMessage);
			miniViewState.TipIsOk = false;
			miniViewState.TipIsWarnTone = true;
		}
		else if (num)
		{
			miniViewState.TipStatus = (flag ? "实时同步 · 新周期" : ((_snapshot == null || string.IsNullOrWhiteSpace(_snapshot.StatusMessage)) ? "实时同步" : _snapshot.StatusMessage));
			miniViewState.TipIsOk = true;
			miniViewState.TipIsWarnTone = false;
		}
		else if (shortDisplay.Exists || weekDisplay.Exists)
		{
			miniViewState.TipStatus = "快照已过期 · 使用后同步";
			miniViewState.TipIsOk = false;
			miniViewState.TipIsWarnTone = true;
		}
		else
		{
			miniViewState.TipStatus = ((_snapshot == null || string.IsNullOrWhiteSpace(_snapshot.StatusMessage)) ? "等待新快照" : _snapshot.StatusMessage);
			miniViewState.TipIsOk = false;
			miniViewState.TipIsWarnTone = true;
		}
		List<string> list = new List<string>();
		if (planBadge.ShowBadge)
		{
			list.Add("套餐 " + planBadge.Title + " · " + planBadge.Subtitle);
		}
		if (shortDisplay.Exists && weekDisplay.Exists)
		{
			list.Add(shortDisplay.Label + " " + UsageFormatter.WindowDetail(shortDisplay, now));
			list.Add(weekDisplay.Label + " " + UsageFormatter.WindowDetail(weekDisplay, now));
		}
		else if (weekDisplay.Exists)
		{
			list.Add(weekDisplay.Label + " " + UsageFormatter.WindowDetail(weekDisplay, now));
		}
		else if (shortDisplay.Exists)
		{
			list.Add(shortDisplay.Label + " " + UsageFormatter.WindowDetail(shortDisplay, now));
		}
		miniViewState.TipDetail = ((list.Count > 0) ? string.Join("\n", list.ToArray()) : "等待额度数据");
		return miniViewState;
	}

	private void UpdateTray(WindowDisplay shortDisplay, WindowDisplay weekDisplay, bool force)
	{
		string text = ((shortDisplay.Exists && weekDisplay.Exists) ? ("Codex Orbit · " + shortDisplay.Label + " " + shortDisplay.RoundedPercent + "% · " + weekDisplay.Label + " " + weekDisplay.RoundedPercent + "%") : (weekDisplay.Exists ? ("Codex Orbit · " + weekDisplay.Label + " " + weekDisplay.RoundedPercent + "%") : ((!shortDisplay.Exists) ? "Codex Orbit · 等待额度数据" : ("Codex Orbit · " + shortDisplay.Label + " " + shortDisplay.RoundedPercent + "%"))));
		if (text.Length > 63)
		{
			text = text.Substring(0, 63);
		}
		if (!string.Equals(text, _lastTrayText, StringComparison.Ordinal))
		{
			_lastTrayText = text;
			_trayIcon.Text = text;
		}
		WindowDisplay windowDisplay = (weekDisplay.Exists ? weekDisplay : shortDisplay);
		int num = (windowDisplay.Exists ? windowDisplay.RoundedPercent : (-1));
		string trayIconStyle = _settings.TrayIconStyle;
		string text2 = trayIconStyle + "|" + num + "|" + windowDisplay.Alert.ToString() + "|" + ((ThemeManager.Current == null) ? "" : ThemeManager.Current.Id);
		if (force || !string.Equals(text2, _lastTrayIconKey, StringComparison.Ordinal))
		{
			_lastTrayIconKey = text2;
			Icon icon;
			if (num < 0 || trayIconStyle == "logo")
			{
				icon = TrayIconRenderer.LoadLogoIcon();
			}
			else
			{
				ThemePalette current = ThemeManager.Current;
				System.Windows.Media.Color accent = ((windowDisplay.Alert == AlertLevel.Danger) ? ThemeManager.ParseColor(current.Danger) : ((windowDisplay.Alert == AlertLevel.Warn) ? ThemeManager.ParseColor(current.Warn) : ThemeManager.ParseColor(current.AccentWeek)));
				icon = ((trayIconStyle == "ring") ? TrayIconRenderer.RenderRing(num, accent) : TrayIconRenderer.RenderNumber(num, accent));
			}
			Icon icon2 = _trayIcon.Icon;
			_trayIcon.Icon = icon;
			icon2?.Dispose();
		}
	}

	private void CheckNotifications()
	{
		if (!_isPreview && _snapshot != null && _snapshot.IsLive)
		{
			DateTimeOffset now = DateTimeOffset.Now;
			long trackedResetsAtTicks = _settings.TrackedWeekResetsAtTicks;
			long num = _settings.NotifiedWeekDeadlineTicks;
			bool flag = ResetNotifyTracker.Evaluate(_snapshot.WeekWindow, now, ref trackedResetsAtTicks, num, out var notifyDeadlineTicks, out var trackingChanged);
			bool flag2 = trackingChanged;
			if (flag && _settings.NotifyOnReset)
			{
				num = notifyDeadlineTicks;
				flag2 = true;
				ShowBalloon("额度已重置", "周额度已进入新周期，可以继续使用了", ToolTipIcon.Info);
			}
			else if (flag)
			{
				num = notifyDeadlineTicks;
				flag2 = true;
			}
			if (flag2)
			{
				_settings.TrackedWeekResetsAtTicks = trackedResetsAtTicks;
				_settings.NotifiedWeekDeadlineTicks = num;
				_settings.Save();
			}
			WindowDisplay windowDisplay = WindowDisplay.FromShort(_snapshot.ShortWindow, now);
			if (windowDisplay.Exists)
			{
				CheckLowQuotaNotification(_snapshot.ShortWindow, windowDisplay, now, ref _warnedCycleShort);
			}
			WindowDisplay windowDisplay2 = WindowDisplay.From(_snapshot.WeekWindow, now);
			if (windowDisplay2.Exists)
			{
				CheckLowQuotaNotification(_snapshot.WeekWindow, windowDisplay2, now, ref _warnedCycleWeek);
			}
		}
	}

	private void CheckLowQuotaNotification(UsageWindowSnapshot window, WindowDisplay display, DateTimeOffset now, ref long warnedCycle)
	{
		if (window != null && display.Exists)
		{
			int lowQuotaNotifyPercent = _settings.LowQuotaNotifyPercent;
			long utcTicks = window.ResetsAt.UtcTicks;
			if (lowQuotaNotifyPercent > 0 && display.Stage == QuotaStage.Fresh && !display.IsUnused && display.Percent <= (double)lowQuotaNotifyPercent && warnedCycle != utcTicks)
			{
				warnedCycle = utcTicks;
				ShowBalloon("额度偏低", display.Label + " 额度仅剩 " + display.RoundedPercent + "%，" + UsageFormatter.CountdownLong(window.ResetsAt - now) + "重置", ToolTipIcon.Warning);
			}
		}
	}

	private void ShowBalloon(string title, string message, ToolTipIcon icon)
	{
		try
		{
			_trayIcon.ShowBalloonTip(4500, title, message, icon);
		}
		catch
		{
		}
	}

	private void LayoutGauge()
	{
		double num = Math.Min((base.ActualWidth > 0.0) ? base.ActualWidth : base.Width, (base.ActualHeight > 0.0) ? base.ActualHeight : base.Height);
		if (!(num <= 0.0) && !(Math.Abs(num - _appliedLayoutSize) < 0.4))
		{
			_appliedLayoutSize = num;
			double num2 = Math.Max(0.8, Math.Min(3.0, num / 120.0));
			System.Windows.Point center = new System.Windows.Point(base.ActualWidth / 2.0, base.ActualHeight / 2.0);
			double num3 = num * 0.385;
			double num4 = Math.Max(4.5, 7.0 * num2);
			double num5 = Math.Max(4.0, 6.0 * num2);
			double num6 = Math.Max(1.0, (num4 + num5) / 2.0 - 0.75 * num2);
			double num7 = num3 - num6;
			double num8 = num3 + num4 / 2.0 + Math.Max(1.8, 2.4 * num2);
			double num9 = Math.Max(1.1, 1.35 * num2);
			ConfigureDisc(CenterBackdrop, center, num8 + num9 / 2.0);
			ConfigureRing(EdgeRim, center, num8, num9);
			ConfigureRing(WeekTrack, center, num3, num4);
			ConfigureRing(ShortTrack, center, num7, num5);
			WeekRing.StrokeThickness = num4;
			ShortRing.StrokeThickness = num5;
			WeekRing.Data = CreateArcGeometry(center, num3, _appliedWeekPct);
			ShortRing.Data = (_appliedShortVisible ? CreateArcGeometry(center, num7, _appliedShortPct) : Geometry.Empty);
			WeekPercent.FontSize = 16.5 * num2;
			ShortPercent.FontSize = 14.5 * num2;
			ResetText.FontSize = 8.5 * num2;
			ValuePanel.MaxWidth = num7 * 2.0 - 8.0;
			bool flag = num >= 132.0;
			ResetText.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			ValuePanelShift.Y = (flag ? (-5.5 * num2) : 0.0);
			if (flag)
			{
				double num10 = 22.0 * num2;
				double num11 = num7 - num5 / 2.0 - 2.0;
				double num12 = ((num11 > num10) ? Math.Sqrt(num11 * num11 - num10 * num10) : 0.0);
				ResetText.MaxWidth = Math.Max(34.0, num12 * 2.0);
			}
		}
	}

	private static void ConfigureRing(Ellipse ellipse, System.Windows.Point center, double radius, double stroke)
	{
		ellipse.Width = radius * 2.0;
		ellipse.Height = radius * 2.0;
		ellipse.StrokeThickness = stroke;
		Canvas.SetLeft(ellipse, center.X - radius);
		Canvas.SetTop(ellipse, center.Y - radius);
	}

	private static void ConfigureDisc(Ellipse ellipse, System.Windows.Point center, double radius)
	{
		ellipse.Width = radius * 2.0;
		ellipse.Height = radius * 2.0;
		Canvas.SetLeft(ellipse, center.X - radius);
		Canvas.SetTop(ellipse, center.Y - radius);
	}

	private static Geometry CreateArcGeometry(System.Windows.Point center, double radius, double percent)
	{
		percent = Math.Max(0.0, Math.Min(100.0, percent));
		if (percent <= 0.01)
		{
			return Geometry.Empty;
		}
		if (percent >= 99.99)
		{
			EllipseGeometry ellipseGeometry = new EllipseGeometry(center, radius, radius);
			ellipseGeometry.Freeze();
			return ellipseGeometry;
		}
		double num = -90.0;
		double angleDegrees = num + 360.0 * percent / 100.0;
		System.Windows.Point startPoint = PointOnCircle(center, radius, num);
		System.Windows.Point point = PointOnCircle(center, radius, angleDegrees);
		PathFigure pathFigure = new PathFigure
		{
			StartPoint = startPoint,
			IsClosed = false,
			IsFilled = false
		};
		pathFigure.Segments.Add(new ArcSegment
		{
			Point = point,
			Size = new System.Windows.Size(radius, radius),
			SweepDirection = SweepDirection.Clockwise,
			IsLargeArc = (percent > 50.0)
		});
		PathGeometry pathGeometry = new PathGeometry(new PathFigure[1] { pathFigure });
		pathGeometry.Freeze();
		return pathGeometry;
	}

	private static System.Windows.Point PointOnCircle(System.Windows.Point center, double radius, double angleDegrees)
	{
		double num = angleDegrees * Math.PI / 180.0;
		return new System.Windows.Point(center.X + radius * Math.Cos(num), center.Y + radius * Math.Sin(num));
	}

	private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (message != 132)
		{
			return IntPtr.Zero;
		}
		long num = lParam.ToInt64();
		int num2 = (short)(num & 0xFFFF);
		int num3 = (short)((num >> 16) & 0xFFFF);
		System.Windows.Point point;
		try
		{
			point = PointFromScreen(new System.Windows.Point(num2, num3));
		}
		catch (InvalidOperationException)
		{
			return IntPtr.Zero;
		}
		double num4 = base.ActualWidth / 2.0;
		double num5 = base.ActualHeight / 2.0;
		double num6 = point.X - num4;
		double num7 = point.Y - num5;
		double num8 = Math.Sqrt(num6 * num6 + num7 * num7);
		double num9 = Math.Min(base.ActualWidth, base.ActualHeight) / 2.0;
		if (num8 > num9 + 1.0)
		{
			handled = true;
			return new IntPtr(-1);
		}
		double num10 = Math.Max(10.0, Math.Min(18.0, num9 * 0.16));
		if (num8 < num9 - num10)
		{
			return IntPtr.Zero;
		}
		double num11 = Math.Atan2(num7, num6) * 180.0 / Math.PI;
		int value = ((num11 >= -22.5 && num11 < 22.5) ? 11 : ((num11 >= 22.5 && num11 < 67.5) ? 17 : ((num11 >= 67.5 && num11 < 112.5) ? 15 : ((num11 >= 112.5 && num11 < 157.5) ? 16 : ((num11 >= 157.5 || num11 < -157.5) ? 10 : ((num11 >= -157.5 && num11 < -112.5) ? 13 : ((!(num11 >= -112.5) || !(num11 < -67.5)) ? 14 : 12)))))));
		handled = true;
		return new IntPtr(value);
	}

	private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ButtonState == MouseButtonState.Pressed)
		{
			CloseDetailToolTip();
			double left = base.Left;
			double top = base.Top;
			try
			{
				DragMove();
			}
			catch (InvalidOperationException)
			{
			}
			if (Math.Abs(base.Left - left) > 0.5 || Math.Abs(base.Top - top) > 0.5)
			{
				SaveRingBounds();
			}
		}
	}

	private void Root_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
		{
			CloseDetailToolTip();
			ShowContextMenu();
			return;
		}
		if (DetailToolTip.IsOpen)
		{
			CloseDetailToolTip();
			return;
		}
		DetailToolTip.PlacementTarget = Root;
		DetailToolTip.IsOpen = true;
		_detailTipTimer.Stop();
		_detailTipTimer.Start();
	}

	private void CloseDetailToolTip()
	{
		_detailTipTimer.Stop();
		DetailToolTip.IsOpen = false;
	}

	private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (_maintainingAspectRatio || !base.IsLoaded)
		{
			_appliedLayoutSize = -1.0;
			LayoutGauge();
			return;
		}
		double num = Math.Abs(e.NewSize.Width - e.PreviousSize.Width);
		double num2 = Math.Abs(e.NewSize.Height - e.PreviousSize.Height);
		double val = ((num >= num2) ? e.NewSize.Width : e.NewSize.Height);
		val = Math.Max(base.MinWidth, Math.Min(val, 480.0));
		_maintainingAspectRatio = true;
		base.Width = val;
		base.Height = val;
		_maintainingAspectRatio = false;
		_appliedLayoutSize = -1.0;
		LayoutGauge();
		_resizeSaveTimer.Stop();
		_resizeSaveTimer.Start();
	}

	private void StartEntranceAnimation()
	{
		Root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220.0)));
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.965, 1.0, TimeSpan.FromMilliseconds(260.0))
		{
			EasingFunction = easingFunction
		});
		RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.965, 1.0, TimeSpan.FromMilliseconds(260.0))
		{
			EasingFunction = easingFunction
		});
	}

	private void RestoreWindowSettings()
	{
		double val = (double.IsNaN(_settings.RingSize) ? 120.0 : _settings.RingSize);
		val = (base.Width = Math.Max(base.MinWidth, Math.Min(val, 480.0)));
		base.Height = val;
		if (!double.IsNaN(_settings.RingLeft) && !double.IsNaN(_settings.RingTop) && IsVisiblePosition(_settings.RingLeft, _settings.RingTop))
		{
			base.Left = _settings.RingLeft;
			base.Top = _settings.RingTop;
		}
		else
		{
			base.WindowStartupLocation = WindowStartupLocation.Manual;
			base.Left = SystemParameters.WorkArea.Right - base.Width - 24.0;
			base.Top = SystemParameters.WorkArea.Bottom - base.Height - 106.0;
		}
		base.Topmost = _settings.Topmost;
		_miniStatusWindow.Topmost = _settings.Topmost;
	}

	private static bool IsVisiblePosition(double left, double top)
	{
		if (left + 80.0 >= SystemParameters.VirtualScreenLeft && left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 80.0 && top + 60.0 >= SystemParameters.VirtualScreenTop)
		{
			return top <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 60.0;
		}
		return false;
	}

	private void SaveRingBounds()
	{
		if (!double.IsNaN(base.Left) && !double.IsNaN(base.Top))
		{
			_settings.RingLeft = base.Left;
			_settings.RingTop = base.Top;
			_settings.RingSize = ((base.ActualWidth > 0.0) ? base.ActualWidth : base.Width);
			_settings.Topmost = base.Topmost;
			_settings.Save();
		}
	}

	private void ApplyDisplayMode()
	{
		ApplyDisplayMode(initialBoot: false);
	}

	private void ApplyDisplayMode(bool initialBoot)
	{
		if (_suppressedByFullscreen)
		{
			if (base.IsVisible)
			{
				Hide();
			}
			_miniStatusWindow.HideStatus();
			return;
		}
		string displayMode = _settings.DisplayMode;
		bool num = displayMode == "Ring" || displayMode == "Both";
		bool flag = displayMode == "Mini" || displayMode == "Both";
		if (num)
		{
			if (!base.IsVisible | initialBoot)
			{
				if (!base.IsVisible)
				{
					Show();
				}
				_appliedLayoutSize = -1.0;
				LayoutGauge();
				StartEntranceAnimation();
			}
		}
		else if (base.IsVisible)
		{
			Hide();
		}
		if (flag)
		{
			ShowMiniWindow();
		}
		else
		{
			_miniStatusWindow.HideStatus();
		}
	}

	private void UpdateFullscreenSuppression()
	{
		if (_isPreview)
		{
			return;
		}
		bool flag = _settings.HideWhenFullscreen && FullscreenDetector.IsForeignFullscreen();
		if (flag == _suppressedByFullscreen)
		{
			return;
		}
		_suppressedByFullscreen = flag;
		if (flag)
		{
			if (base.IsVisible)
			{
				Hide();
			}
			_miniStatusWindow.HideStatus();
		}
		else
		{
			ApplyDisplayMode(initialBoot: false);
		}
	}

	private void ShowMiniWindow()
	{
		GetCurrentScreenRects(out var workingArea, out var screenBounds);
		_miniStatusWindow.Topmost = _settings.Topmost;
		_miniStatusWindow.ShowNearTaskbar(workingArea, screenBounds);
	}

	public void RevealFromExternal()
	{
		ApplyDisplayMode();
		if (_miniStatusWindow.IsVisible)
		{
			_miniStatusWindow.Reveal();
		}
		if (base.IsVisible && base.Topmost)
		{
			base.Topmost = false;
			base.Topmost = true;
		}
	}

	private void GetCurrentScreenRects(out Rect workingArea, out Rect screenBounds)
	{
		workingArea = SystemParameters.WorkArea;
		screenBounds = new Rect(0.0, 0.0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
		if (!base.IsVisible)
		{
			return;
		}
		PresentationSource presentationSource = PresentationSource.FromVisual(this);
		if (presentationSource != null && presentationSource.CompositionTarget != null)
		{
			System.Windows.Point point;
			try
			{
				point = PointToScreen(new System.Windows.Point(base.ActualWidth / 2.0, base.ActualHeight / 2.0));
			}
			catch (InvalidOperationException)
			{
				return;
			}
			Screen screen = Screen.FromPoint(new System.Drawing.Point((int)Math.Round(point.X), (int)Math.Round(point.Y)));
			Matrix transformFromDevice = presentationSource.CompositionTarget.TransformFromDevice;
			System.Windows.Point point2 = transformFromDevice.Transform(new System.Windows.Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
			System.Windows.Point point3 = transformFromDevice.Transform(new System.Windows.Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
			System.Windows.Point point4 = transformFromDevice.Transform(new System.Windows.Point(screen.Bounds.Left, screen.Bounds.Top));
			System.Windows.Point point5 = transformFromDevice.Transform(new System.Windows.Point(screen.Bounds.Right, screen.Bounds.Bottom));
			workingArea = new Rect(point2, point3);
			screenBounds = new Rect(point4, point5);
		}
	}

	private void TrayIcon_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
	{
		if (e.Button == MouseButtons.Right)
		{
			base.Dispatcher.BeginInvoke(new Action(ShowContextMenu));
		}
		else if (e.Button == MouseButtons.Left)
		{
			base.Dispatcher.BeginInvoke(new Action(RevealFromExternal));
		}
	}

	private void ShowContextMenu()
	{
		UpdateMenuChecks();
		IntPtr intPtr = IntPtr.Zero;
		if (_miniStatusWindow.IsVisible && _miniStatusWindow.WindowHandle != IntPtr.Zero)
		{
			intPtr = _miniStatusWindow.WindowHandle;
		}
		else if (base.IsVisible && _windowSource != null)
		{
			intPtr = _windowSource.Handle;
		}
		if (intPtr != IntPtr.Zero)
		{
			NativeMethods.SetForegroundWindow(intPtr);
		}
		_menu.Placement = PlacementMode.MousePoint;
		_menu.IsOpen = true;
	}

	private void BuildContextMenu()
	{
		_menu = new System.Windows.Controls.ContextMenu();
		System.Windows.Controls.MenuItem newItem = new System.Windows.Controls.MenuItem
		{
			Header = "Codex Orbit  ·  v" + typeof(MainWindow).Assembly.GetName().Version.ToString(3),
			IsEnabled = false
		};
		_menu.Items.Add(newItem);
		_menu.Items.Add(new Separator());
		System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem
		{
			Header = "显示模式"
		};
		AddRadioItem(menuItem, _modeItems, "Mini", "迷你悬浮窗", delegate
		{
			SetDisplayMode("Mini");
		});
		AddRadioItem(menuItem, _modeItems, "Ring", "圆环悬浮窗", delegate
		{
			SetDisplayMode("Ring");
		});
		AddRadioItem(menuItem, _modeItems, "Both", "同时显示", delegate
		{
			SetDisplayMode("Both");
		});
		_menu.Items.Add(menuItem);
		System.Windows.Controls.MenuItem menuItem2 = new System.Windows.Controls.MenuItem
		{
			Header = "主题"
		};
		foreach (ThemePalette preset in ThemeManager.Presets)
		{
			ThemePalette captured = preset;
			System.Windows.Controls.MenuItem menuItem3 = new System.Windows.Controls.MenuItem
			{
				Header = preset.Name
			};
			menuItem3.Click += delegate
			{
				SetTheme(captured.Id);
			};
			_themeItems[preset.Id] = menuItem3;
			menuItem2.Items.Add(menuItem3);
		}
		_menu.Items.Add(menuItem2);
		System.Windows.Controls.MenuItem menuItem4 = new System.Windows.Controls.MenuItem
		{
			Header = "不透明度"
		};
		int[] array = new int[4] { 100, 85, 70, 55 };
		for (int num = 0; num < array.Length; num++)
		{
			int num2 = array[num];
			int captured2 = num2;
			AddRadioItem(menuItem4, _opacityItems, num2, num2 + "%", delegate
			{
				SetOpacityPercent(captured2);
			});
		}
		_menu.Items.Add(menuItem4);
		System.Windows.Controls.MenuItem menuItem5 = new System.Windows.Controls.MenuItem
		{
			Header = "托盘图标"
		};
		AddRadioItem(menuItem5, _trayStyleItems, "percent", "数字百分比", delegate
		{
			SetTrayStyle("percent");
		});
		AddRadioItem(menuItem5, _trayStyleItems, "ring", "进度圆环", delegate
		{
			SetTrayStyle("ring");
		});
		AddRadioItem(menuItem5, _trayStyleItems, "logo", "经典图标", delegate
		{
			SetTrayStyle("logo");
		});
		_menu.Items.Add(menuItem5);
		System.Windows.Controls.MenuItem menuItem6 = new System.Windows.Controls.MenuItem
		{
			Header = "套餐标识"
		};
		AddRadioItem(menuItem6, _planItems, "auto", "自动识别", delegate
		{
			SetPlanOverride("auto");
		});
		AddRadioItem(menuItem6, _planItems, "free", "Free 免费", delegate
		{
			SetPlanOverride("free");
		});
		AddRadioItem(menuItem6, _planItems, "plus", "Plus", delegate
		{
			SetPlanOverride("plus");
		});
		AddRadioItem(menuItem6, _planItems, "pro5x", "Pro 5×", delegate
		{
			SetPlanOverride("pro5x");
		});
		AddRadioItem(menuItem6, _planItems, "pro20x", "Pro 20×", delegate
		{
			SetPlanOverride("pro20x");
		});
		AddRadioItem(menuItem6, _planItems, "go", "Go", delegate
		{
			SetPlanOverride("go");
		});
		AddRadioItem(menuItem6, _planItems, "business", "Business", delegate
		{
			SetPlanOverride("business");
		});
		AddRadioItem(menuItem6, _planItems, "enterprise", "Enterprise / Edu", delegate
		{
			SetPlanOverride("enterprise");
		});
		_menu.Items.Add(menuItem6);
		_menu.Items.Add(new Separator());
		System.Windows.Controls.MenuItem menuItem7 = new System.Windows.Controls.MenuItem
		{
			Header = "低额度提醒"
		};
		AddRadioItem(menuItem7, _notifyItems, 0, "关闭", delegate
		{
			SetNotifyThreshold(0);
		});
		AddRadioItem(menuItem7, _notifyItems, 5, "剩余 5% 时提醒", delegate
		{
			SetNotifyThreshold(5);
		});
		AddRadioItem(menuItem7, _notifyItems, 10, "剩余 10% 时提醒", delegate
		{
			SetNotifyThreshold(10);
		});
		AddRadioItem(menuItem7, _notifyItems, 20, "剩余 20% 时提醒", delegate
		{
			SetNotifyThreshold(20);
		});
		_menu.Items.Add(menuItem7);
		_resetNotifyItem = new System.Windows.Controls.MenuItem
		{
			Header = "额度重置提醒"
		};
		_resetNotifyItem.Click += delegate
		{
			_settings.NotifyOnReset = !_settings.NotifyOnReset;
			_settings.Save();
			UpdateMenuChecks();
		};
		_menu.Items.Add(_resetNotifyItem);
		_menu.Items.Add(new Separator());
		_topmostItem = new System.Windows.Controls.MenuItem
		{
			Header = "始终置顶"
		};
		_topmostItem.Click += delegate
		{
			_settings.Topmost = !_settings.Topmost;
			base.Topmost = _settings.Topmost;
			_miniStatusWindow.Topmost = _settings.Topmost;
			_settings.Save();
			UpdateMenuChecks();
		};
		_menu.Items.Add(_topmostItem);
		_hideFullscreenItem = new System.Windows.Controls.MenuItem
		{
			Header = "全屏时自动隐藏"
		};
		_hideFullscreenItem.Click += delegate
		{
			_settings.HideWhenFullscreen = !_settings.HideWhenFullscreen;
			_settings.Save();
			if (!_settings.HideWhenFullscreen)
			{
				_suppressedByFullscreen = false;
				ApplyDisplayMode(initialBoot: false);
			}
			else
			{
				UpdateFullscreenSuppression();
			}
			UpdateMenuChecks();
		};
		_menu.Items.Add(_hideFullscreenItem);
		_clickThroughItem = new System.Windows.Controls.MenuItem
		{
			Header = "鼠标穿透（经托盘菜单恢复）"
		};
		_clickThroughItem.Click += delegate
		{
			SetClickThrough(!_settings.ClickThrough);
		};
		_menu.Items.Add(_clickThroughItem);
		_autoStartItem = new System.Windows.Controls.MenuItem
		{
			Header = "开机自启"
		};
		_autoStartItem.Click += delegate
		{
			AutoStart.SetEnabled(!AutoStart.IsEnabled());
			UpdateMenuChecks();
		};
		_menu.Items.Add(_autoStartItem);
		_menu.Items.Add(new Separator());
		System.Windows.Controls.MenuItem menuItem8 = new System.Windows.Controls.MenuItem
		{
			Header = "立即刷新"
		};
		menuItem8.Click += delegate
		{
			_reader.RequestRefresh(forceLive: true);
		};
		_menu.Items.Add(menuItem8);
		System.Windows.Controls.MenuItem menuItem9 = new System.Windows.Controls.MenuItem
		{
			Header = "打开会话日志目录"
		};
		menuItem9.Click += delegate
		{
			OpenSessionsFolder();
		};
		_menu.Items.Add(menuItem9);
		_menu.Items.Add(new Separator());
		System.Windows.Controls.MenuItem menuItem10 = new System.Windows.Controls.MenuItem
		{
			Header = "退出 Codex Orbit"
		};
		menuItem10.Click += delegate
		{
			ExitApplication();
		};
		_menu.Items.Add(menuItem10);
		UpdateMenuChecks();
	}

	private static void AddRadioItem<TKey>(System.Windows.Controls.MenuItem parent, Dictionary<TKey, System.Windows.Controls.MenuItem> registry, TKey key, string header, Action onClick)
	{
		System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem
		{
			Header = header
		};
		menuItem.Click += delegate
		{
			onClick();
		};
		registry[key] = menuItem;
		parent.Items.Add(menuItem);
	}

	private void UpdateMenuChecks()
	{
		foreach (KeyValuePair<string, System.Windows.Controls.MenuItem> modeItem in _modeItems)
		{
			modeItem.Value.IsChecked = string.Equals(modeItem.Key, _settings.DisplayMode, StringComparison.OrdinalIgnoreCase);
		}
		foreach (KeyValuePair<int, System.Windows.Controls.MenuItem> opacityItem in _opacityItems)
		{
			opacityItem.Value.IsChecked = opacityItem.Key == _settings.OpacityPercent;
		}
		foreach (KeyValuePair<string, System.Windows.Controls.MenuItem> trayStyleItem in _trayStyleItems)
		{
			trayStyleItem.Value.IsChecked = string.Equals(trayStyleItem.Key, _settings.TrayIconStyle, StringComparison.OrdinalIgnoreCase);
		}
		string b = (string.IsNullOrWhiteSpace(_settings.PlanOverride) ? "auto" : _settings.PlanOverride);
		foreach (KeyValuePair<string, System.Windows.Controls.MenuItem> planItem in _planItems)
		{
			planItem.Value.IsChecked = string.Equals(planItem.Key, b, StringComparison.OrdinalIgnoreCase);
		}
		foreach (KeyValuePair<int, System.Windows.Controls.MenuItem> notifyItem in _notifyItems)
		{
			notifyItem.Value.IsChecked = notifyItem.Key == _settings.LowQuotaNotifyPercent;
		}
		_resetNotifyItem.IsChecked = _settings.NotifyOnReset;
		_topmostItem.IsChecked = _settings.Topmost;
		_hideFullscreenItem.IsChecked = _settings.HideWhenFullscreen;
		_clickThroughItem.IsChecked = _settings.ClickThrough;
		_autoStartItem.IsChecked = AutoStart.IsEnabled();
		foreach (KeyValuePair<string, System.Windows.Controls.MenuItem> themeItem in _themeItems)
		{
			bool selected = string.Equals(themeItem.Key, _settings.Theme, StringComparison.OrdinalIgnoreCase);
			themeItem.Value.Icon = BuildThemeDot(ThemeManager.Find(themeItem.Key), selected);
		}
	}

	private static object BuildThemeDot(ThemePalette palette, bool selected)
	{
		Grid grid = new Grid
		{
			Width = 14.0,
			Height = 14.0
		};
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush(ThemeManager.ParseColor(palette.WeekA), ThemeManager.ParseColor(palette.WeekB), 45.0);
		linearGradientBrush.Freeze();
		grid.Children.Add(new Ellipse
		{
			Width = 10.0,
			Height = 10.0,
			Fill = linearGradientBrush,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		});
		if (selected)
		{
			Ellipse ellipse = new Ellipse
			{
				Width = 14.0,
				Height = 14.0,
				StrokeThickness = 1.3
			};
			ellipse.SetResourceReference(Shape.StrokeProperty, "ThMenuAccent");
			grid.Children.Add(ellipse);
		}
		return grid;
	}

	private void OpenSessionsFolder()
	{
		try
		{
			string sessionsPath = _reader.SessionsPath;
			if (Directory.Exists(sessionsPath))
			{
				Process.Start("explorer.exe", "\"" + sessionsPath + "\"");
			}
			else
			{
				ShowBalloon("目录不存在", "未找到 " + sessionsPath, ToolTipIcon.Info);
			}
		}
		catch
		{
		}
	}

	private void SetDisplayMode(string mode)
	{
		_settings.DisplayMode = mode;
		_settings.Save();
		ApplyDisplayMode();
		UpdateMenuChecks();
	}

	private void SetTheme(string id)
	{
		ThemeManager.Apply(id);
		_settings.Theme = ThemeManager.Current.Id;
		_settings.Save();
		_lastTrayIconKey = null;
		RefreshUi(force: true);
		UpdateMenuChecks();
	}

	private void SetOpacityPercent(int percent)
	{
		_settings.OpacityPercent = percent;
		_settings.Save();
		double opacity = (base.Opacity = Math.Max(0.4, (double)percent / 100.0));
		_miniStatusWindow.Opacity = opacity;
		UpdateMenuChecks();
	}

	private void SetTrayStyle(string style)
	{
		_settings.TrayIconStyle = style;
		_settings.Save();
		_lastTrayIconKey = null;
		RefreshUi(force: false);
		UpdateMenuChecks();
	}

	private void SetPlanOverride(string planOverride)
	{
		_settings.PlanOverride = planOverride;
		_settings.Save();
		RefreshUi(force: true);
		UpdateMenuChecks();
	}

	private void SetNotifyThreshold(int percent)
	{
		_settings.LowQuotaNotifyPercent = percent;
		_settings.Save();
		UpdateMenuChecks();
	}

	private void SetClickThrough(bool enabled)
	{
		_settings.ClickThrough = enabled;
		_settings.Save();
		if (_windowSource != null)
		{
			NativeMethods.SetClickThrough(_windowSource.Handle, enabled);
		}
		_miniStatusWindow.ApplyClickThrough(enabled);
		UpdateMenuChecks();
		if (enabled)
		{
			ShowBalloon("鼠标穿透已开启", "悬浮窗不再响应鼠标，可在托盘图标右键菜单中关闭", ToolTipIcon.Info);
		}
	}

	private static void SetText(TextBlock target, string value)
	{
		if (!string.Equals(target.Text, value, StringComparison.Ordinal))
		{
			target.Text = value;
		}
	}

	private static void ApplyBrushKey(FrameworkElement element, DependencyProperty property, string key, ref string appliedKey)
	{
		if (!string.Equals(key, appliedKey, StringComparison.Ordinal))
		{
			appliedKey = key;
			element.SetResourceReference(property, key);
		}
	}

	private static string RingBrushKey(AlertLevel alert, bool isWeek)
	{
		switch (alert)
		{
		case AlertLevel.Danger:
			return "ThDangerRing";
		case AlertLevel.Warn:
			return "ThWarnRing";
		default:
			if (!isWeek)
			{
				return "ThShortRing";
			}
			return "ThWeekRing";
		}
	}

	private static string TextBrushKey(AlertLevel alert, bool isWeek)
	{
		switch (alert)
		{
		case AlertLevel.Danger:
			return "ThDangerText";
		case AlertLevel.Warn:
			return "ThWarnText";
		default:
			if (!isWeek)
			{
				return "ThAccentShort";
			}
			return "ThAccentWeek";
		}
	}

	private static UsageSnapshot CreateDemoSnapshot()
	{
		DateTimeOffset now = DateTimeOffset.Now;
		return new UsageSnapshot
		{
			WeekWindow = new UsageWindowSnapshot
			{
				WindowMinutes = 10080,
				UsedPercent = 57.0,
				ResetsAt = now.AddDays(6.0).AddHours(20.0),
				ObservedAt = now,
				LimitId = "codex"
			},
			PlanType = "pro",
			StatusMessage = "预览数据"
		};
	}

	private void RenderPreview(string path)
	{
		Root.BeginAnimation(UIElement.OpacityProperty, null);
		RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
		RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
		Root.Opacity = 1.0;
		RootScale.ScaleX = 1.0;
		RootScale.ScaleY = 1.0;
		UpdateLayout();
		const double previewScale = 2.0;
		int pixelWidth = Math.Max(1, (int)Math.Ceiling(base.ActualWidth * previewScale));
		int pixelHeight = Math.Max(1, (int)Math.Ceiling(base.ActualHeight * previewScale));
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96.0 * previewScale, 96.0 * previewScale, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(this);
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
		string directoryName = System.IO.Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		using FileStream stream = File.Create(path);
		pngBitmapEncoder.Save(stream);
	}
}
