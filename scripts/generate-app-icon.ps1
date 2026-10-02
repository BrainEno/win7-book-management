param(
    [string]$OutputPath = "src\Win7BookManagement\Resources\BookDesk.ico"
)

$ErrorActionPreference = "Stop"

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class BookDeskIconGenerator
{
    private sealed class Frame
    {
        public int Size;
        public byte[] Bytes;
    }

    public static void Generate(string outputPath)
    {
        var sizes = new[] { 16, 20, 24, 32, 48, 64, 128, 256 };
        var frames = new List<Frame>();

        foreach (var size in sizes)
        {
            frames.Add(new Frame
            {
                Size = size,
                Bytes = RenderPng(size)
            });
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)frames.Count);

            var offset = 6 + (frames.Count * 16);
            foreach (var frame in frames)
            {
                writer.Write((byte)(frame.Size >= 256 ? 0 : frame.Size));
                writer.Write((byte)(frame.Size >= 256 ? 0 : frame.Size));
                writer.Write((byte)0);
                writer.Write((byte)0);
                writer.Write((ushort)1);
                writer.Write((ushort)32);
                writer.Write((uint)frame.Bytes.Length);
                writer.Write((uint)offset);
                offset += frame.Bytes.Length;
            }

            foreach (var frame in frames)
                writer.Write(frame.Bytes);

            writer.Flush();
            File.WriteAllBytes(outputPath, stream.ToArray());
        }
    }

    private static byte[] RenderPng(int size)
    {
        using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            var scale = size / 1024f;
            var blue = Color.FromArgb(255, 22, 119, 255);
            var blueInner = Color.FromArgb(255, 36, 132, 255);
            var white = Color.White;
            var spine = Color.FromArgb(255, 235, 244, 255);
            var line = Color.FromArgb(255, 168, 204, 255);
            var gold = Color.FromArgb(255, 255, 190, 55);
            var shelf = Color.FromArgb(235, 255, 255, 255);

            FillRounded(graphics, Rect(72, 72, 880, 880, scale), S(190, scale), blue);
            FillRounded(graphics, Rect(108, 108, 808, 808, scale), S(160, scale), blueInner);

            using (var brush = new SolidBrush(white))
            {
                graphics.FillPolygon(brush, Points(scale,
                    210,300, 470,350, 470,730, 230,685, 180,625, 180,325));
                graphics.FillPolygon(brush, Points(scale,
                    554,350, 814,300, 844,325, 844,625, 794,685, 554,730));
            }

            FillRounded(graphics, Rect(486, 330, 52, 430, scale), S(24, scale), spine);

            using (var brush = new SolidBrush(line))
            {
                foreach (var y in new[] { 410, 475, 540, 605 })
                {
                    FillRounded(graphics, Rect(245, y, 185, 18, scale), S(9, scale), brush);
                    FillRounded(graphics, Rect(594, y, 185, 18, scale), S(9, scale), brush);
                }
            }

            using (var brush = new SolidBrush(gold))
            {
                graphics.FillPolygon(brush, Points(scale,
                    690,250, 770,250, 770,410, 730,378, 690,410));
            }

            FillRounded(graphics, Rect(250, 782, 524, 42, scale), S(21, scale), shelf);

            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }
    }

    private static RectangleF Rect(int x, int y, int width, int height, float scale)
    {
        return new RectangleF(
            S(x, scale),
            S(y, scale),
            S(width, scale),
            S(height, scale));
    }

    private static float S(int value, float scale)
    {
        return value * scale;
    }

    private static PointF[] Points(float scale, params int[] values)
    {
        var points = new PointF[values.Length / 2];
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = new PointF(
                S(values[i * 2], scale),
                S(values[i * 2 + 1], scale));
        }
        return points;
    }

    private static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var diameter = radius * 2f;
        var path = new GraphicsPath();
        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180f, 90f);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270f, 90f);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    private static void FillRounded(Graphics graphics, RectangleF rect, float radius, Color color)
    {
        using (var brush = new SolidBrush(color))
            FillRounded(graphics, rect, radius, brush);
    }

    private static void FillRounded(Graphics graphics, RectangleF rect, float radius, Brush brush)
    {
        using (var path = Rounded(rect, radius))
            graphics.FillPath(brush, path);
    }
}
"@

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$target = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path $root $OutputPath
}

[BookDeskIconGenerator]::Generate($target)
Write-Host "BOOK DESK application icon generated: $target"
