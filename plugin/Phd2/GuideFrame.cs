using System;
using System.Globalization;
using System.Text;

namespace NinaHaTelemetry.Phd2 {

    public static class GuideFrame {
        private const int FitsBlock = 2880;
        private const int CardLength = 80;
        private const int LutSize = 65536;
        private const int StatsSampleSize = 200_000;
        private static readonly (byte B, byte G, byte R) LockColor = (87, 166, 216);
        private static readonly (byte B, byte G, byte R) StarColor = (140, 158, 111);

        public sealed class Image {
            public int Width, Height;
            public float[] Pixels;
            public double FullScale;
        }

        public sealed record Rendered(int Width, int Height, byte[] Bgr);

        public static Image ParseFits(byte[] bytes) {
            if (bytes.Length < FitsBlock || Encoding.ASCII.GetString(bytes, 0, 6) != "SIMPLE") throw new FormatException("not a FITS file");
            int bitpix = 0, naxis = 0, width = 0, height = 0;
            double bzero = 0, bscale = 1;
            int pos = 0;
            bool end = false;
            while (!end && pos + CardLength <= bytes.Length) {
                string card = Encoding.ASCII.GetString(bytes, pos, CardLength);
                pos += CardLength;
                string key = card.Substring(0, 8).Trim();
                string value = card[8] == '=' ? card.Substring(10).Split('/')[0].Trim() : "";
                switch (key) {
                    case "BITPIX": bitpix = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "NAXIS": naxis = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "NAXIS1": width = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "NAXIS2": height = int.Parse(value, CultureInfo.InvariantCulture); break;
                    case "BZERO": bzero = double.Parse(value, CultureInfo.InvariantCulture); break;
                    case "BSCALE": bscale = double.Parse(value, CultureInfo.InvariantCulture); break;
                    case "END": end = true; break;
                }
            }
            if (!end || naxis < 2 || width <= 0 || height <= 0) throw new FormatException("unsupported FITS header");

            int dataStart = (pos + FitsBlock - 1) / FitsBlock * FitsBlock;
            int bytesPerPixel = Math.Abs(bitpix) / 8;
            int count = width * height;
            if ((long)dataStart + (long)count * bytesPerPixel > bytes.Length) throw new FormatException("truncated FITS data");

            var pixels = new float[count];
            double max = 0;
            for (int i = 0, o = dataStart; i < count; i++, o += bytesPerPixel) {
                double raw = bitpix switch {
                    8 => bytes[o],
                    16 => (short)((bytes[o] << 8) | bytes[o + 1]),
                    32 => BigEndianInt32(bytes, o),
                    -32 => BitConverter.Int32BitsToSingle(BigEndianInt32(bytes, o)),
                    _ => throw new FormatException($"BITPIX {bitpix} not supported"),
                };
                double v = bzero + bscale * raw;
                pixels[i] = (float)v;
                if (v > max) max = v;
            }
            double fullScale = bitpix == 16 && bzero == 32768 ? 65535 : bitpix == 8 ? 255 : Math.Max(max, 1);
            return new Image { Width = width, Height = height, Pixels = pixels, FullScale = fullScale };
        }

        public static Rendered Render(Image image, int maxWidth, (double X, double Y)? lockPosition, (double X, double Y)? star) {
            int k = Math.Max(1, (int)Math.Ceiling(image.Width / (double)Math.Max(64, maxWidth)));
            int w = image.Width / k, h = image.Height / k;
            var lut = BuildStretch(image);
            var bgr = new byte[w * h * 3];
            for (int y = 0; y < h; y++) {
                for (int x = 0; x < w; x++) {
                    double sum = 0;
                    for (int dy = 0; dy < k; dy++) {
                        int rowStart = (y * k + dy) * image.Width + x * k;
                        for (int dx = 0; dx < k; dx++) sum += image.Pixels[rowStart + dx];
                    }
                    double v = Math.Clamp(sum / (k * k) / image.FullScale, 0, 1);
                    byte grey = lut[(int)(v * (LutSize - 1))];
                    int o = (y * w + x) * 3;
                    bgr[o] = bgr[o + 1] = bgr[o + 2] = grey;
                }
            }
            if (lockPosition is { } lp) Ring(bgr, w, h, lp.X / k, lp.Y / k, Math.Max(9, 28.0 / k), LockColor);
            if (star is { } sp) Cross(bgr, w, h, sp.X / k, sp.Y / k, Math.Max(5, 14.0 / k), StarColor);
            return new Rendered(w, h, bgr);
        }

        private static byte[] BuildStretch(Image image) {
            var (median, mad, lo, hi) = Statistics(image);
            double shadows = Math.Clamp(median - 2.8 * mad, 0, 1);
            var lut = new byte[LutSize];
            if (hi - lo < 2.0 / 65535) {
                Array.Fill(lut, ToByte(median));
            } else if (mad < 1e-6 || 1 - shadows < 1e-4) {
                for (int i = 0; i < LutSize; i++) lut[i] = ToByte((Level(i) - lo) / (hi - lo));
            } else {
                double midtones = Mtf(0.25, median - shadows);
                for (int i = 0; i < LutSize; i++) lut[i] = ToByte(Mtf(midtones, Math.Clamp((Level(i) - shadows) / (1 - shadows), 0, 1)));
            }
            return lut;
        }

        private static (double Median, double Mad, double Lo, double Hi) Statistics(Image image) {
            int stride = Math.Max(1, image.Pixels.Length / StatsSampleSize);
            var sample = new float[(image.Pixels.Length + stride - 1) / stride];
            for (int i = 0, j = 0; i < image.Pixels.Length; i += stride, j++) sample[j] = image.Pixels[i] / (float)image.FullScale;
            Array.Sort(sample);
            double median = sample[sample.Length / 2];
            var deviations = new float[sample.Length];
            for (int i = 0; i < sample.Length; i++) deviations[i] = Math.Abs(sample[i] - (float)median);
            Array.Sort(deviations);
            return (median, deviations[deviations.Length / 2] * 1.4826, sample[0], sample[^1]);
        }

        private static double Level(int lutIndex) => lutIndex / (double)(LutSize - 1);

        private static byte ToByte(double unit) => (byte)Math.Round(255 * Math.Clamp(unit, 0, 1));

        private static double Mtf(double m, double x) =>
            x <= 0 ? 0 : x >= 1 ? 1 : x == m ? 0.5 : (m - 1) * x / ((2 * m - 1) * x - m);

        private static int BigEndianInt32(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

        private static void Put(byte[] bgr, int w, int h, int x, int y, (byte B, byte G, byte R) color) {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            int o = (y * w + x) * 3;
            bgr[o] = color.B;
            bgr[o + 1] = color.G;
            bgr[o + 2] = color.R;
        }

        private static void Ring(byte[] bgr, int w, int h, double cx, double cy, double radius, (byte, byte, byte) color) {
            int steps = (int)(4 * Math.PI * radius);
            for (int i = 0; i < steps; i++) {
                double angle = 2 * Math.PI * i / steps;
                for (int t = -1; t <= 1; t++)
                    Put(bgr, w, h, (int)Math.Round(cx + (radius + t) * Math.Cos(angle)), (int)Math.Round(cy + (radius + t) * Math.Sin(angle)), color);
            }
        }

        private static void Cross(byte[] bgr, int w, int h, double cx, double cy, double radius, (byte, byte, byte) color) {
            int gap = (int)Math.Max(2, radius / 3);
            int x0 = (int)cx, y0 = (int)cy;
            for (int d = gap; d <= radius; d++) {
                for (int t = -1; t <= 1; t++) {
                    Put(bgr, w, h, x0 + d, y0 + t, color);
                    Put(bgr, w, h, x0 - d, y0 + t, color);
                    Put(bgr, w, h, x0 + t, y0 + d, color);
                    Put(bgr, w, h, x0 + t, y0 - d, color);
                }
            }
        }
    }
}
