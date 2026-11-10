using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NinaHaTelemetry.Phd2;

namespace NinaHaTelemetry.Telemetry {

    internal static class JpegEncoder {
        private const int Quality = 80;

        public static byte[] Encode(BitmapSource source, int maxWidth) {
            BitmapSource bitmap = source;
            if (source.PixelWidth > maxWidth) {
                double scale = (double)maxWidth / source.PixelWidth;
                bitmap = new TransformedBitmap(source, new ScaleTransform(scale, scale));
            }
            var encoder = new JpegBitmapEncoder { QualityLevel = Quality };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            return stream.ToArray();
        }

        public static byte[] Encode(GuideFrame.Rendered frame) {
            var bitmap = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr24, null, frame.Bgr, frame.Width * 3);
            bitmap.Freeze();
            return Encode(bitmap, frame.Width);
        }
    }
}
