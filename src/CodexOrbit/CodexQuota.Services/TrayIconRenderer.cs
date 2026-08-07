using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;
using System.Windows.Media;

namespace CodexQuota.Services;

public static class TrayIconRenderer
{
	public const string StylePercent = "percent";

	public const string StyleRing = "ring";

	public const string StyleLogo = "logo";

	public static Icon RenderNumber(int percent, System.Windows.Media.Color accent)
	{
		using Bitmap bitmap = new Bitmap(32, 32);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			Prepare(graphics);
			System.Drawing.Color color = ToDrawing(accent);
			string text = ((percent >= 100) ? "100" : Math.Max(0, percent).ToString());
			float emSize = ((text.Length >= 3) ? 13f : ((text.Length == 2) ? 17f : 19f));
			using (Font font = new Font("Segoe UI", emSize, FontStyle.Bold, GraphicsUnit.Pixel))
			{
				using StringFormat format = new StringFormat
				{
					Alignment = StringAlignment.Center,
					LineAlignment = StringAlignment.Center
				};
				RectangleF rectangleF = new RectangleF(0f, 1.2f, 32f, 32f);
				using (SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(190, 8, 10, 16)))
				{
					for (int i = -1; i <= 1; i++)
					{
						for (int j = -1; j <= 1; j++)
						{
							if (i != 0 || j != 0)
							{
								RectangleF layoutRectangle = rectangleF;
								layoutRectangle.Offset(i, j);
								graphics.DrawString(text, font, brush, layoutRectangle, format);
							}
						}
					}
				}
				using SolidBrush brush2 = new SolidBrush(color);
				graphics.DrawString(text, font, brush2, rectangleF, format);
			}
			int num = (int)Math.Round((double)(26 * Math.Max(0, Math.Min(100, percent))) / 100.0);
			using (SolidBrush brush3 = new SolidBrush(System.Drawing.Color.FromArgb(90, 128, 138, 160)))
			{
				graphics.FillRectangle(brush3, 3, 28, 26, 3);
			}
			if (num > 0)
			{
				using SolidBrush brush4 = new SolidBrush(color);
				graphics.FillRectangle(brush4, 3, 28, num, 3);
			}
		}
		return FromBitmap(bitmap);
	}

	public static Icon RenderRing(int percent, System.Windows.Media.Color accent)
	{
		using Bitmap bitmap = new Bitmap(32, 32);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			Prepare(graphics);
			System.Drawing.Color color = ToDrawing(accent);
			RectangleF rect = new RectangleF(4f, 4f, 24f, 24f);
			using (System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(110, 128, 138, 160), 5f))
			{
				graphics.DrawEllipse(pen, rect);
			}
			float num = (float)(360.0 * (double)Math.Max(0, Math.Min(100, percent)) / 100.0);
			if (num > 0.5f)
			{
				using System.Drawing.Pen pen2 = new System.Drawing.Pen(color, 5f)
				{
					StartCap = LineCap.Round,
					EndCap = LineCap.Round
				};
				graphics.DrawArc(pen2, rect, -90f, num);
			}
			using SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(170, color));
			graphics.FillEllipse(brush, 13f, 13f, 6f, 6f);
		}
		return FromBitmap(bitmap);
	}

	public static Icon LoadLogoIcon()
	{
		using (Stream stream = typeof(TrayIconRenderer).Assembly.GetManifestResourceStream("CodexQuota.Assets.tray-icon.ico"))
		{
			if (stream != null)
			{
				using (Icon icon = new Icon(stream))
				{
					return (Icon)icon.Clone();
				}
			}
		}
		try
		{
			using Icon icon2 = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
			if (icon2 != null)
			{
				return (Icon)icon2.Clone();
			}
		}
		catch
		{
		}
		return (Icon)SystemIcons.Application.Clone();
	}

	private static void Prepare(Graphics g)
	{
		g.SmoothingMode = SmoothingMode.AntiAlias;
		g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
		g.PixelOffsetMode = PixelOffsetMode.HighQuality;
		g.Clear(System.Drawing.Color.Transparent);
	}

	private static System.Drawing.Color ToDrawing(System.Windows.Media.Color color)
	{
		return System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
	}

	private static Icon FromBitmap(Bitmap bitmap)
	{
		IntPtr hicon = bitmap.GetHicon();
		try
		{
			using Icon icon = Icon.FromHandle(hicon);
			return (Icon)icon.Clone();
		}
		finally
		{
			NativeMethods.DestroyIcon(hicon);
		}
	}
}
