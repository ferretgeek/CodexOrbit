using System;
using System.Runtime.InteropServices;

namespace CodexQuota.Services;

internal static class NativeMethods
{
	public const int GwlExStyle = -20;

	public const long WsExTransparent = 32L;

	public const long WsExToolWindow = 128L;

	public const long WsExNoActivate = 134217728L;

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
	private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
	private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
	private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
	private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DestroyIcon(IntPtr handle);

	private static long GetExStyle(IntPtr hwnd)
	{
		if (IntPtr.Size != 8)
		{
			return GetWindowLong32(hwnd, -20);
		}
		return GetWindowLongPtr64(hwnd, -20).ToInt64();
	}

	private static void SetExStyle(IntPtr hwnd, long value)
	{
		if (IntPtr.Size == 8)
		{
			SetWindowLongPtr64(hwnd, -20, new IntPtr(value));
		}
		else
		{
			SetWindowLong32(hwnd, -20, (int)value);
		}
	}

	public static void ApplyWidgetStyles(IntPtr hwnd, bool clickThrough)
	{
		if (!(hwnd == IntPtr.Zero))
		{
			long exStyle = GetExStyle(hwnd);
			exStyle |= 0x8000080;
			exStyle = ((!clickThrough) ? (exStyle & -33) : (exStyle | 0x20));
			SetExStyle(hwnd, exStyle);
		}
	}

	public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
	{
		if (!(hwnd == IntPtr.Zero))
		{
			long exStyle = GetExStyle(hwnd);
			exStyle = ((!clickThrough) ? (exStyle & -33) : (exStyle | 0x20));
			SetExStyle(hwnd, exStyle);
		}
	}
}
