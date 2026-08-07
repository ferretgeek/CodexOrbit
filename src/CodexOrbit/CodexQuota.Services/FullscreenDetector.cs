using System;
using System.Runtime.InteropServices;

namespace CodexQuota.Services;

public static class FullscreenDetector
{
	private enum QueryUserNotificationState
	{
		NotPresent = 1,
		Busy,
		RunningD3dFullScreen,
		PresentationMode,
		AcceptsNotifications,
		QuietTime,
		App
	}

	private struct Rect
	{
		public int Left;

		public int Top;

		public int Right;

		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private struct MonitorInfo
	{
		public int Size;

		public Rect Monitor;

		public Rect Work;

		public uint Flags;
	}

	private const int MonitorDefaultToNearest = 2;

	private const int TolerancePx = 8;

	private static readonly uint CurrentProcessId = GetCurrentProcessId();

	[DllImport("shell32.dll")]
	private static extern int SHQueryUserNotificationState(out QueryUserNotificationState state);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsWindowVisible(IntPtr hWnd);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

	[DllImport("user32.dll", CharSet = CharSet.Auto)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("kernel32.dll")]
	private static extern uint GetCurrentProcessId();

	public static bool IsForeignFullscreen()
	{
		try
		{
			if (SHQueryUserNotificationState(out var state) == 0 && (state == QueryUserNotificationState.RunningD3dFullScreen || state == QueryUserNotificationState.PresentationMode || state == QueryUserNotificationState.Busy))
			{
				return true;
			}
			return IsForegroundCoveringMonitor();
		}
		catch
		{
			return false;
		}
	}

	private static bool IsForegroundCoveringMonitor()
	{
		IntPtr foregroundWindow = GetForegroundWindow();
		if (foregroundWindow == IntPtr.Zero || !IsWindowVisible(foregroundWindow))
		{
			return false;
		}
		GetWindowThreadProcessId(foregroundWindow, out var lpdwProcessId);
		if (lpdwProcessId == 0 || lpdwProcessId == CurrentProcessId)
		{
			return false;
		}
		if (!GetWindowRect(foregroundWindow, out var lpRect))
		{
			return false;
		}
		IntPtr intPtr = MonitorFromWindow(foregroundWindow, 2u);
		if (intPtr == IntPtr.Zero)
		{
			return false;
		}
		MonitorInfo lpmi = new MonitorInfo
		{
			Size = Marshal.SizeOf(typeof(MonitorInfo))
		};
		if (!GetMonitorInfo(intPtr, ref lpmi))
		{
			return false;
		}
		Rect monitor = lpmi.Monitor;
		if (!Covers(lpRect, monitor, 8))
		{
			return false;
		}
		Rect work = lpmi.Work;
		if ((work.Left != monitor.Left || work.Top != monitor.Top || work.Right != monitor.Right || work.Bottom != monitor.Bottom) && Covers(lpRect, work, 8) && !LargerThan(lpRect, work, 8))
		{
			return false;
		}
		return true;
	}

	private static bool Covers(Rect window, Rect area, int tol)
	{
		if (window.Left <= area.Left + tol && window.Top <= area.Top + tol && window.Right >= area.Right - tol)
		{
			return window.Bottom >= area.Bottom - tol;
		}
		return false;
	}

	private static bool LargerThan(Rect window, Rect area, int tol)
	{
		if (window.Left >= area.Left - tol && window.Top >= area.Top - tol && window.Right <= area.Right + tol)
		{
			return window.Bottom > area.Bottom + tol;
		}
		return true;
	}
}
