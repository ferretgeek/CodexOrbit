using System;

namespace CodexQuota.Services;

internal static class WindowPlacement
{
	public static double ResizeKeepingNearestEdge(double left, double oldWidth, double newWidth, double workingLeft, double workingRight)
	{
		if (!IsFinite(left) || !IsFinite(oldWidth) || !IsFinite(newWidth) || !IsFinite(workingLeft) || !IsFinite(workingRight) || oldWidth <= 0.0 || newWidth <= 0.0 || workingRight <= workingLeft)
		{
			return left;
		}
		double previousRight = left + oldWidth;
		double distanceFromLeft = Math.Abs(left - workingLeft);
		double distanceFromRight = Math.Abs(workingRight - previousRight);
		double anchoredLeft = (distanceFromRight < distanceFromLeft) ? (previousRight - newWidth) : left;
		double maximumLeft = workingRight - newWidth;
		if (maximumLeft < workingLeft)
		{
			return workingLeft;
		}
		return Math.Max(workingLeft, Math.Min(anchoredLeft, maximumLeft));
	}

	private static bool IsFinite(double value)
	{
		return !double.IsNaN(value) && !double.IsInfinity(value);
	}
}
