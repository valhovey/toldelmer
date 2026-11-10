# Guide frame rendering

`Phd2/GuideFrame.cs` turns a PHD2 FITS into a small annotated BGR24 bitmap. It is pure C# (no WPF, no NINA), so the [[Build and test|offline harness]] tests it on a real PHD2 frame. The plugin only adds JPEG encoding. Part of [[PHD2 guide frame]].

- **FITS:** primary HDU, 2-D, BITPIX 8/16/32/-32, BZERO/BSCALE applied. Full scale is 65535 for unsigned 16-bit (BITPIX 16 + BZERO 32768), 255 for 8-bit, otherwise the data max.
- **Downscale:** a k×k block average with k = ⌈width / max(64, maxWidth)⌉, so 1920×1080 becomes 640×360 at 800 px.
- **Stretch:** an auto-STF from a subsample of about 200 k pixels. Shadows clip at median − 2.8 × 1.4826 × MAD, and the PixInsight midtones transfer function maps the median to 25 % grey. It uses a 65536-step LUT: a dark-sky background sits within a few ADU, and a 4096-step LUT quantised it to black.

## Gotchas
- **Degenerate frames:** a uniformly saturated daylight frame (every pixel 65520, a 12-bit camera's ceiling) put the STF black point at the median and rendered all black, while PHD2 showed it white. Now a flat frame (range < 2 ADU) renders at its true level, and a frame with zero MAD (mostly clipped) falls back to a linear min..max stretch. The harness checks saturated, zero, mostly-clipped and faint-night synthetic frames.
- **Row order:** PHD2 writes its buffer as-is (`fits_write_pix` from pixel 1), so the first FITS row is the **top** of PHD2's display. That is against the FITS convention, which is bottom-up. Don't flip rows. The lock and star coordinates (top-left origin) match only unflipped. A FITS viewer shows these frames upside down.
