using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using CodexQuota.Services;

namespace CodexQuota;

public partial class MiniStatusWindow : Window, IComponentConnector
{
	private enum MiniDockSide
	{
		None,
		Left,
		Right
	}

	private const double GaugeOnlyWidth = 128.0;

	private const double BarWithPlanWidth = 248.0;

	private const double BarWithShortAndPlanWidth = 368.0;

	private const double BarWithShortWidth = 256.0;

	private const double HandleWidth = 48.0;

	private const double DockThreshold = 72.0;

	private const double ExpandedHeight = 96.0;

	private const double HandleTrackHeight = 56.0;

	private readonly AppSettings _settings;

	private readonly Action _showMenuAction;

	private readonly DispatcherTimer _collapseTimer;

	private readonly DispatcherTimer _infoTipTimer;

	private Rect _workingArea;

	private Rect _screenBounds;

	private bool _hasPosition;

	private double _expandedWidth = BarWithShortWidth;

	private bool _isCollapsed;

	private bool _suppressRevealUntilMouseLeave;

	private bool _dockCollapseTransition;

	private MiniDockSide _dockSide;

	private HwndSource _source;

	private Storyboard _planShimmerStoryboard;

	private double _appliedGaugePct = -1.0;

	private bool _appliedGaugeValid;

	private bool _appliedShowPill;

	private bool _appliedShowPlan;

	private PlanKind _appliedPlanKind;

	private string _appliedThemeId = "";

	private string _gaugeStrokeKey = "ThWeekRing";

	private string _gaugeLabelKey = "ThAccentWeek";

	private string _pillValueKey = "ThAccentShort";

	private string _handleFillKey = "ThHandleFill";

	private string _handlePercentKey = "ThAccentWeek";

	private string _tipStatusKey = "ThOk";

	public IntPtr WindowHandle
	{
		get
		{
			if (_source == null)
			{
				return IntPtr.Zero;
			}
			return _source.Handle;
		}
	}

	public MiniStatusWindow(AppSettings settings, Action showMenuAction)
	{
		InitializeComponent();
		_settings = settings;
		_showMenuAction = showMenuAction;
		_dockSide = ParseDockSide(settings.MiniDock);
		_collapseTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(650.0)
		};
		_collapseTimer.Tick += CollapseTimer_Tick;
		_infoTipTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(3.0)
		};
		_infoTipTimer.Tick += delegate
		{
			CloseInfoToolTip();
		};
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		_source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
		if (_source != null)
		{
			NativeMethods.ApplyWidgetStyles(_source.Handle, _settings.ClickThrough);
		}
		base.Opacity = Math.Max(0.4, (double)_settings.OpacityPercent / 100.0);
	}

	public void ApplyClickThrough(bool enabled)
	{
		if (_source != null)
		{
			NativeMethods.SetClickThrough(_source.Handle, enabled);
		}
	}

	public void Reveal()
	{
		_collapseTimer.Stop();
		_suppressRevealUntilMouseLeave = false;
		_dockCollapseTransition = false;
		if (!base.IsVisible)
		{
			Show();
		}
		if (_dockSide != MiniDockSide.None && _isCollapsed)
		{
			SetCollapsed(collapsed: false, animate: true);
		}
	}

	public void ApplyState(MiniViewState state, bool force)
	{
		if ((state.ShowPill != _appliedShowPill || state.ShowPlanBadge != _appliedShowPlan) | force)
		{
			_appliedShowPill = state.ShowPill;
			_appliedShowPlan = state.ShowPlanBadge;
			PillShell.Visibility = ((!state.ShowPill) ? Visibility.Collapsed : Visibility.Visible);
			PlanBadgeShell.Visibility = ((!state.ShowPlanBadge) ? Visibility.Collapsed : Visibility.Visible);
			PlanDivider.Visibility = ((!state.ShowPlanBadge) ? Visibility.Collapsed : Visibility.Visible);
			ApplyExpandedLayout(state.ShowPlanBadge, state.ShowPill);
		}
		string text = ((ThemeManager.Current == null) ? "" : ThemeManager.Current.Id);
		if (force || text != _appliedThemeId || state.PlanKind != _appliedPlanKind || !string.Equals(PlanTitleText.Text, state.PlanTitle, StringComparison.Ordinal))
		{
			_appliedThemeId = text;
			_appliedPlanKind = state.PlanKind;
			ApplyPlanVisual(state);
		}
		SetText(ShortLabelText, state.PillLabel);
		SetText(ShortValueText, state.PillValue);
		SetText(ShortResetText, state.PillReset);
		ApplyBrushKey(ShortValueText, TextBlock.ForegroundProperty, TextKey(state.PillAlert, isWeek: false), ref _pillValueKey);
		SetText(GaugeValueText, state.GaugeValue);
		SetText(GaugeLabelText, state.GaugeLabel);
		SetText(GaugeResetText, state.GaugeReset);
		SetText(HandlePercentText, state.GaugeValid ? state.GaugeValue : "--");
		ApplyBrushKey(GaugeLabelText, TextBlock.ForegroundProperty, state.GaugeIsWeek ? "ThAccentWeek" : "ThAccentShort", ref _gaugeLabelKey);
		ApplyBrushKey(HandlePercentText, TextBlock.ForegroundProperty, TextKey(state.GaugeAlert, state.GaugeIsWeek), ref _handlePercentKey);
		ApplyBrushKey(GaugeProgressPath, Shape.StrokeProperty, RingKey(state.GaugeAlert, state.GaugeIsWeek), ref _gaugeStrokeKey);
		ApplyBrushKey(HandleProgress, Border.BackgroundProperty, HandleKey(state.GaugeAlert), ref _handleFillKey);
		double num = (state.GaugeValid ? Math.Max(0.0, Math.Min(100.0, state.GaugePercent)) : 0.0);
		if (force || state.GaugeValid != _appliedGaugeValid || Math.Abs(num - _appliedGaugePct) > 0.05)
		{
			_appliedGaugeValid = state.GaugeValid;
			_appliedGaugePct = num;
			UpdateGaugeArc(num, state.GaugeValid);
			HandleProgress.Height = (state.GaugeValid ? Math.Max(5.0, 56.0 * num / 100.0) : 5.0);
		}
		SetText(DetailStatusText, state.TipStatus);
		ApplyBrushKey(DetailStatusText, TextBlock.ForegroundProperty, state.TipIsOk ? "ThOk" : "ThWarnText", ref _tipStatusKey);
		SetText(DetailValueText, state.TipDetail);
	}

	private void ApplyExpandedLayout(bool showPlan, bool showPill)
	{
		double previousWidth = _expandedWidth;
		double expandedWidth = ((showPlan & showPill) ? BarWithShortAndPlanWidth : (showPlan ? BarWithPlanWidth : ((!showPill) ? GaugeOnlyWidth : BarWithShortWidth)));
		_expandedWidth = expandedWidth;
		ExpandedContent.Width = _expandedWidth;
		base.Height = 96.0;
		if (!_isCollapsed)
		{
			base.Width = _expandedWidth;
			if (_dockSide != MiniDockSide.None && base.IsVisible)
			{
				PositionDocked(collapsed: false);
			}
			else if (base.IsVisible && _workingArea.Width > 0.0)
			{
				base.Left = WindowPlacement.ResizeKeepingNearestEdge(base.Left, previousWidth, _expandedWidth, _workingArea.Left, _workingArea.Right);
			}
		}
	}

	private void ApplyPlanVisual(MiniViewState state)
	{
		SetText(PlanTitleText, state.PlanTitle);
		SetText(PlanSubText, state.PlanSubtitle);
		SetText(PlanGlyphText, state.PlanGlyph);
		ThemePalette current = ThemeManager.Current;
		if (current != null)
		{
			System.Windows.Media.Color color = ThemeManager.ParseColor(current.Surface);
			System.Windows.Media.Color color2 = ThemeManager.ParseColor(current.SurfaceBorder);
			System.Windows.Media.Color color3 = ThemeManager.ParseColor(current.WeekA);
			System.Windows.Media.Color c = ThemeManager.ParseColor(current.WeekB);
			System.Windows.Media.Color color4 = ThemeManager.ParseColor(current.TextHi);
			System.Windows.Media.Color c2 = ThemeManager.ParseColor(current.TextLo);
			System.Windows.Media.Color color5 = ThemeManager.ParseColor(current.AccentWeek);
			System.Windows.Media.Color color6 = ThemeManager.ParseColor(current.Glow);
			System.Windows.Media.Color b = ThemeManager.ParseColor(current.Bg1);
			bool flag = state.PlanKind == PlanKind.Pro20x;
			System.Windows.Media.Color startColor = (flag ? WithAlpha(color3, 232) : color2);
			System.Windows.Media.Color endColor = (flag ? WithAlpha(c, 200) : WithAlpha(color2, 176));
			System.Windows.Media.Color color7 = color;
			System.Windows.Media.Color color8 = Blend(color, b, 0.35);
			LinearGradientBrush linearGradientBrush = new LinearGradientBrush(startColor, endColor, 0.0);
			linearGradientBrush.Freeze();
			UnifiedShell.BorderBrush = linearGradientBrush;
			LinearGradientBrush linearGradientBrush2 = new LinearGradientBrush
			{
				StartPoint = new System.Windows.Point(0.0, 0.5),
				EndPoint = new System.Windows.Point(1.0, 0.5)
			};
			linearGradientBrush2.GradientStops.Add(new GradientStop(color7, 0.0));
			linearGradientBrush2.GradientStops.Add(new GradientStop(color8, 1.0));
			linearGradientBrush2.Freeze();
			UnifiedShell.Background = linearGradientBrush2;
			PlanDivider.Background = new SolidColorBrush(WithAlpha(color5, 102));
			PlanTitleText.Foreground = new SolidColorBrush(color4);
			PlanSubText.Foreground = new SolidColorBrush(WithAlpha(c2, 240));
			PlanGlyphText.Foreground = new SolidColorBrush(color5);
			UnifiedShadow.Color = color6;
			UnifiedShadow.Opacity = (flag ? 0.55 : 0.38);
			UnifiedShadow.BlurRadius = (flag ? 18 : 14);
			ApplyThemeShimmerBrush(color3);
			if (flag)
			{
				StartPlanShimmer();
			}
			else
			{
				StopPlanShimmer();
			}
		}
	}

	private void ApplyThemeShimmerBrush(System.Windows.Media.Color accent)
	{
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush
		{
			StartPoint = new System.Windows.Point(0.0, 0.5),
			EndPoint = new System.Windows.Point(1.0, 0.5)
		};
		linearGradientBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, accent.R, accent.G, accent.B), 0.0));
		linearGradientBrush.GradientStops.Add(new GradientStop(WithAlpha(accent, 102), 0.5));
		linearGradientBrush.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0, accent.R, accent.G, accent.B), 1.0));
		linearGradientBrush.Freeze();
		PlanShimmer.Background = linearGradientBrush;
	}

	private void StartPlanShimmer()
	{
		StopPlanShimmer();
		PlanShimmer.Opacity = 0.7;
		TranslateTransform translateTransform = new TranslateTransform(-80.0, 0.0);
		PlanShimmer.RenderTransform = translateTransform;
		DoubleAnimation doubleAnimation = new DoubleAnimation(-120.0, 260.0, TimeSpan.FromSeconds(1.8))
		{
			EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseInOut
			}
		};
		DoubleAnimationUsingKeyFrames doubleAnimationUsingKeyFrames = new DoubleAnimationUsingKeyFrames();
		doubleAnimationUsingKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
		doubleAnimationUsingKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.75, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.65))));
		doubleAnimationUsingKeyFrames.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.8))));
		_planShimmerStoryboard = new Storyboard();
		Storyboard.SetTarget(doubleAnimation, translateTransform);
		Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath(TranslateTransform.XProperty));
		Storyboard.SetTarget(doubleAnimationUsingKeyFrames, PlanShimmer);
		Storyboard.SetTargetProperty(doubleAnimationUsingKeyFrames, new PropertyPath(UIElement.OpacityProperty));
		_planShimmerStoryboard.Children.Add(doubleAnimation);
		_planShimmerStoryboard.Children.Add(doubleAnimationUsingKeyFrames);
		_planShimmerStoryboard.Completed += delegate
		{
			PlanShimmer.Opacity = 0.0;
			_planShimmerStoryboard = null;
		};
		_planShimmerStoryboard.Begin();
	}

	private static System.Windows.Media.Color WithAlpha(System.Windows.Media.Color c, byte a)
	{
		return System.Windows.Media.Color.FromArgb(a, c.R, c.G, c.B);
	}

	private static System.Windows.Media.Color Blend(System.Windows.Media.Color a, System.Windows.Media.Color b, double t)
	{
		t = Math.Max(0.0, Math.Min(1.0, t));
		return System.Windows.Media.Color.FromArgb((byte)((double)(int)a.A + (double)(b.A - a.A) * t), (byte)((double)(int)a.R + (double)(b.R - a.R) * t), (byte)((double)(int)a.G + (double)(b.G - a.G) * t), (byte)((double)(int)a.B + (double)(b.B - a.B) * t));
	}

	private void StopPlanShimmer()
	{
		if (_planShimmerStoryboard != null)
		{
			_planShimmerStoryboard.Stop();
			_planShimmerStoryboard = null;
		}
		PlanShimmer.BeginAnimation(UIElement.OpacityProperty, null);
		PlanShimmer.Opacity = 0.0;
		PlanShimmer.RenderTransform = null;
	}

	public void ShowNearTaskbar(Rect workingArea, Rect screenBounds)
	{
		_workingArea = workingArea;
		_screenBounds = screenBounds;
		if (!_hasPosition)
		{
			_hasPosition = true;
			if (_dockSide != MiniDockSide.None)
			{
				if (!double.IsNaN(_settings.MiniLeft))
				{
					base.Left = _settings.MiniLeft;
				}
				base.Top = (double.IsNaN(_settings.MiniTop) ? (_workingArea.Bottom - base.Height - 10.0) : _settings.MiniTop);
			}
			else if (!double.IsNaN(_settings.MiniLeft) && !double.IsNaN(_settings.MiniTop) && IsVisiblePosition(_settings.MiniLeft, _settings.MiniTop))
			{
				base.Left = _settings.MiniLeft;
				base.Top = _settings.MiniTop;
			}
			else
			{
				_dockSide = MiniDockSide.None;
				PositionNearTaskbar();
			}
		}
		else if (_dockSide == MiniDockSide.None && !IsVisiblePosition(base.Left, base.Top))
		{
			PositionNearTaskbar();
		}
		if (!base.IsVisible)
		{
			Show();
		}
		UpdateLayout();
		RefreshCurrentScreenRects(useCursor: false);
		base.Top = Clamp(base.Top, _workingArea.Top, _workingArea.Bottom - base.Height);
		if (_dockSide != MiniDockSide.None)
		{
			PositionDocked(collapsed: true);
			SetCollapsed(collapsed: true, animate: false);
		}
		else
		{
			SetCollapsed(collapsed: false, animate: false);
		}
		StartEntranceAnimation();
	}

	public void HideStatus()
	{
		_collapseTimer.Stop();
		CloseInfoToolTip();
		Hide();
	}

	public void RenderPreview(string path)
	{
		_collapseTimer.Stop();
		_dockSide = MiniDockSide.None;
		SetCollapsed(collapsed: false, animate: false);
		Root.BeginAnimation(UIElement.OpacityProperty, null);
		RootTranslate.BeginAnimation(TranslateTransform.YProperty, null);
		Root.Opacity = 1.0;
		RootTranslate.Y = 0.0;
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

	protected override void OnClosed(EventArgs e)
	{
		_collapseTimer.Stop();
		_infoTipTimer.Stop();
		InfoToolTip.IsOpen = false;
		StopPlanShimmer();
		base.OnClosed(e);
	}

	private void PositionNearTaskbar()
	{
		base.Width = _expandedWidth;
		bool flag = _workingArea.Bottom < _screenBounds.Bottom - 1.0;
		bool num = _workingArea.Top > _screenBounds.Top + 1.0;
		bool flag2 = _workingArea.Right < _screenBounds.Right - 1.0;
		bool flag3 = _workingArea.Left > _screenBounds.Left + 1.0;
		if (num)
		{
			base.Left = _workingArea.Right - base.Width - 10.0;
			base.Top = _workingArea.Top + 8.0;
		}
		else if (flag2)
		{
			base.Left = _workingArea.Right - base.Width - 8.0;
			base.Top = _workingArea.Bottom - base.Height - 10.0;
		}
		else if (flag3)
		{
			base.Left = _workingArea.Left + 8.0;
			base.Top = _workingArea.Bottom - base.Height - 10.0;
		}
		else
		{
			base.Left = _workingArea.Right - base.Width - 10.0;
			base.Top = (flag ? (_workingArea.Bottom - base.Height - 8.0) : (_workingArea.Bottom - base.Height - 10.0));
		}
	}

	private void RefreshCurrentScreenRects(bool useCursor)
	{
		if (!base.IsVisible)
		{
			return;
		}
		PresentationSource presentationSource = PresentationSource.FromVisual(this);
		if (presentationSource == null || presentationSource.CompositionTarget == null)
		{
			return;
		}
		System.Drawing.Point point;
		if (useCursor)
		{
			point = System.Windows.Forms.Cursor.Position;
		}
		else
		{
			System.Windows.Point point2;
			try
			{
				point2 = PointToScreen(new System.Windows.Point(Math.Max(1.0, base.ActualWidth / 2.0), base.ActualHeight / 2.0));
			}
			catch (InvalidOperationException)
			{
				return;
			}
			point = new System.Drawing.Point((int)Math.Round(point2.X), (int)Math.Round(point2.Y));
		}
		Screen screen = Screen.FromPoint(point);
		Matrix transformFromDevice = presentationSource.CompositionTarget.TransformFromDevice;
		System.Windows.Point point3 = transformFromDevice.Transform(new System.Windows.Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
		System.Windows.Point point4 = transformFromDevice.Transform(new System.Windows.Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
		System.Windows.Point point5 = transformFromDevice.Transform(new System.Windows.Point(screen.Bounds.Left, screen.Bounds.Top));
		System.Windows.Point point6 = transformFromDevice.Transform(new System.Windows.Point(screen.Bounds.Right, screen.Bounds.Bottom));
		_workingArea = new Rect(point3, point4);
		_screenBounds = new Rect(point5, point6);
	}

	private bool IsVisiblePosition(double left, double top)
	{
		if (double.IsNaN(left) || double.IsInfinity(left) || double.IsNaN(top) || double.IsInfinity(top))
		{
			return false;
		}
		double virtualScreenLeft = SystemParameters.VirtualScreenLeft;
		double virtualScreenTop = SystemParameters.VirtualScreenTop;
		double num = virtualScreenLeft + SystemParameters.VirtualScreenWidth;
		double num2 = virtualScreenTop + SystemParameters.VirtualScreenHeight;
		if (left + _expandedWidth - 24.0 >= virtualScreenLeft && left + 24.0 <= num && top + base.Height - 24.0 >= virtualScreenTop)
		{
			return top + 24.0 <= num2;
		}
		return false;
	}

	private void EvaluateDockAfterDrag()
	{
		RefreshCurrentScreenRects(useCursor: true);
		double num = base.Left + base.ActualWidth;
		if (base.Left <= _workingArea.Left + 72.0)
		{
			_dockSide = MiniDockSide.Left;
		}
		else if (num >= _workingArea.Right - 72.0)
		{
			_dockSide = MiniDockSide.Right;
		}
		else
		{
			_dockSide = MiniDockSide.None;
		}
		base.Top = Clamp(base.Top, _workingArea.Top, _workingArea.Bottom - base.Height);
		if (_dockSide == MiniDockSide.None)
		{
			_suppressRevealUntilMouseLeave = false;
			_dockCollapseTransition = false;
			base.Left = Clamp(base.Left, _workingArea.Left, _workingArea.Right - _expandedWidth);
			SetCollapsed(collapsed: false, animate: false);
		}
		else
		{
			_suppressRevealUntilMouseLeave = true;
			_dockCollapseTransition = true;
			SetCollapsed(collapsed: true, animate: true);
		}
		SaveMiniState();
	}

	private void PositionDocked(bool collapsed)
	{
		double num = (base.Width = (collapsed ? 48.0 : _expandedWidth));
		base.Left = ((_dockSide == MiniDockSide.Right) ? (_workingArea.Right - num) : _workingArea.Left);
		base.Top = Clamp(base.Top, _workingArea.Top, _workingArea.Bottom - base.Height);
	}

	private void SetCollapsed(bool collapsed, bool animate)
	{
		if (_dockSide == MiniDockSide.None)
		{
			collapsed = false;
		}
		_collapseTimer.Stop();
		_isCollapsed = collapsed;
		double targetWidth = (collapsed ? 48.0 : _expandedWidth);
		double targetLeft = base.Left;
		if (_dockSide == MiniDockSide.Right)
		{
			targetLeft = _workingArea.Right - targetWidth;
		}
		else if (_dockSide == MiniDockSide.Left)
		{
			targetLeft = _workingArea.Left;
		}
		ExpandedContent.Visibility = Visibility.Visible;
		CollapsedHandle.Visibility = Visibility.Visible;
		ExpandedContent.IsHitTestVisible = !collapsed;
		if (!animate)
		{
			BeginAnimation(FrameworkElement.WidthProperty, null);
			BeginAnimation(Window.LeftProperty, null);
			ExpandedContent.BeginAnimation(UIElement.OpacityProperty, null);
			CollapsedHandle.BeginAnimation(UIElement.OpacityProperty, null);
			base.Width = targetWidth;
			base.Left = targetLeft;
			ExpandedContent.Opacity = (collapsed ? 0.0 : 1.0);
			CollapsedHandle.Opacity = (collapsed ? 1.0 : 0.0);
			ExpandedContent.Visibility = (collapsed ? Visibility.Collapsed : Visibility.Visible);
			CollapsedHandle.Visibility = ((!collapsed) ? Visibility.Collapsed : Visibility.Visible);
			return;
		}
		double fromValue = ((base.ActualWidth > 0.0) ? base.ActualWidth : base.Width);
		double left = base.Left;
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(190.0);
		DoubleAnimation doubleAnimation = new DoubleAnimation(fromValue, targetWidth, timeSpan)
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation animation = new DoubleAnimation(left, targetLeft, timeSpan)
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation animation2 = new DoubleAnimation(ExpandedContent.Opacity, collapsed ? 0.0 : 1.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		DoubleAnimation animation3 = new DoubleAnimation(CollapsedHandle.Opacity, collapsed ? 1.0 : 0.0, timeSpan)
		{
			EasingFunction = easingFunction
		};
		doubleAnimation.Completed += delegate
		{
			BeginAnimation(FrameworkElement.WidthProperty, null);
			BeginAnimation(Window.LeftProperty, null);
			ExpandedContent.BeginAnimation(UIElement.OpacityProperty, null);
			CollapsedHandle.BeginAnimation(UIElement.OpacityProperty, null);
			base.Width = targetWidth;
			base.Left = targetLeft;
			ExpandedContent.Opacity = (collapsed ? 0.0 : 1.0);
			CollapsedHandle.Opacity = (collapsed ? 1.0 : 0.0);
			ExpandedContent.Visibility = (collapsed ? Visibility.Collapsed : Visibility.Visible);
			CollapsedHandle.Visibility = ((!collapsed) ? Visibility.Collapsed : Visibility.Visible);
			if (_dockCollapseTransition)
			{
				_dockCollapseTransition = false;
				if (!base.IsMouseOver)
				{
					_suppressRevealUntilMouseLeave = false;
				}
			}
		};
		base.Width = targetWidth;
		base.Left = targetLeft;
		BeginAnimation(FrameworkElement.WidthProperty, doubleAnimation);
		BeginAnimation(Window.LeftProperty, animation);
		ExpandedContent.BeginAnimation(UIElement.OpacityProperty, animation2);
		CollapsedHandle.BeginAnimation(UIElement.OpacityProperty, animation3);
	}

	private void ScheduleCollapse()
	{
		if (_dockSide != MiniDockSide.None && !_isCollapsed)
		{
			_collapseTimer.Stop();
			_collapseTimer.Start();
		}
	}

	private void CollapseTimer_Tick(object sender, EventArgs e)
	{
		_collapseTimer.Stop();
		if (_dockSide != MiniDockSide.None && !base.IsMouseOver)
		{
			SetCollapsed(collapsed: true, animate: true);
		}
	}

	private void StartEntranceAnimation()
	{
		CubicEase easingFunction = new CubicEase
		{
			EasingMode = EasingMode.EaseOut
		};
		Root.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(170.0))
		{
			EasingFunction = easingFunction
		});
		RootTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4.0, 0.0, TimeSpan.FromMilliseconds(190.0))
		{
			EasingFunction = easingFunction
		});
	}

	private void Root_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
	{
		_collapseTimer.Stop();
		if (_dockSide != MiniDockSide.None && _isCollapsed && !_suppressRevealUntilMouseLeave)
		{
			SetCollapsed(collapsed: false, animate: true);
		}
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(120.0);
		UnifiedHover.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, timeSpan));
	}

	private void Root_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
	{
		if (_suppressRevealUntilMouseLeave && !_dockCollapseTransition)
		{
			_suppressRevealUntilMouseLeave = false;
		}
		TimeSpan timeSpan = TimeSpan.FromMilliseconds(160.0);
		UnifiedHover.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, timeSpan));
		ScheduleCollapse();
	}

	private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		_collapseTimer.Stop();
		CloseInfoToolTip();
		_suppressRevealUntilMouseLeave = false;
		_dockCollapseTransition = false;
		if (_dockSide != MiniDockSide.None && _isCollapsed)
		{
			SetCollapsed(collapsed: false, animate: false);
		}
		double left = base.Left;
		double top = base.Top;
		try
		{
			DragMove();
		}
		catch (InvalidOperationException)
		{
		}
		double num = Math.Abs(base.Left - left);
		double num2 = Math.Abs(base.Top - top);
		if (num >= SystemParameters.MinimumHorizontalDragDistance || num2 >= SystemParameters.MinimumVerticalDragDistance)
		{
			EvaluateDockAfterDrag();
		}
	}

	private void Root_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
	{
		e.Handled = true;
		if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
		{
			CloseInfoToolTip();
			_showMenuAction?.Invoke();
			return;
		}
		if (InfoToolTip.IsOpen)
		{
			CloseInfoToolTip();
			return;
		}
		InfoToolTip.PlacementTarget = Root;
		InfoToolTip.IsOpen = true;
		_infoTipTimer.Stop();
		_infoTipTimer.Start();
	}

	private void CloseInfoToolTip()
	{
		_infoTipTimer.Stop();
		InfoToolTip.IsOpen = false;
	}

	private void UpdateGaugeArc(double percent, bool valid)
	{
		if (!valid || percent <= 0.0)
		{
			GaugeProgressPath.Data = null;
			return;
		}
		double num = Math.Min(99.999, percent);
		System.Windows.Point startPoint = PointOnCircle(37.5, 37.5, 35.0, -90.0);
		System.Windows.Point point = PointOnCircle(37.5, 37.5, 35.0, -90.0 + num * 3.6);
		PathFigure pathFigure = new PathFigure
		{
			StartPoint = startPoint,
			IsClosed = false
		};
		pathFigure.Segments.Add(new ArcSegment
		{
			Point = point,
			Size = new System.Windows.Size(35.0, 35.0),
			SweepDirection = SweepDirection.Clockwise,
			IsLargeArc = (num > 50.0)
		});
		PathGeometry pathGeometry = new PathGeometry(new PathFigure[1] { pathFigure });
		pathGeometry.Freeze();
		GaugeProgressPath.Data = pathGeometry;
	}

	private void SaveMiniState()
	{
		if (!double.IsNaN(base.Left))
		{
			_settings.MiniLeft = base.Left;
		}
		if (!double.IsNaN(base.Top))
		{
			_settings.MiniTop = base.Top;
		}
		_settings.MiniDock = _dockSide.ToString();
		_settings.Save();
	}

	private static MiniDockSide ParseDockSide(string value)
	{
		if (string.Equals(value, "Left", StringComparison.OrdinalIgnoreCase))
		{
			return MiniDockSide.Left;
		}
		if (string.Equals(value, "Right", StringComparison.OrdinalIgnoreCase))
		{
			return MiniDockSide.Right;
		}
		return MiniDockSide.None;
	}

	private static System.Windows.Point PointOnCircle(double centerX, double centerY, double radius, double angleDegrees)
	{
		double num = angleDegrees * Math.PI / 180.0;
		return new System.Windows.Point(centerX + radius * Math.Cos(num), centerY + radius * Math.Sin(num));
	}

	private static double Clamp(double value, double minimum, double maximum)
	{
		if (maximum < minimum)
		{
			return minimum;
		}
		return Math.Max(minimum, Math.Min(value, maximum));
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

	private static string RingKey(AlertLevel alert, bool isWeek)
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

	private static string TextKey(AlertLevel alert, bool isWeek)
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

	private static string HandleKey(AlertLevel alert)
	{
		return alert switch
		{
			AlertLevel.Danger => "ThHandleFillDanger",
			AlertLevel.Warn => "ThHandleFillWarn",
			_ => "ThHandleFill",
		};
	}
}
