/* Copyright (c) 2026, Purple Pen contributors.
 * All rights reserved.
 */

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Runtime.InteropServices;

namespace AvPurplePen.Controls
{
    /// <summary>Displays the original 24-pixel toolbox icon stored with an OCAD/OpenMapper symbol.</summary>
    public sealed class MapSymbolIconControl : Control
    {
        private WriteableBitmap? bitmap;

        public static readonly StyledProperty<int[]?> PixelsProperty =
            AvaloniaProperty.Register<MapSymbolIconControl, int[]?>(nameof(Pixels));
        public static readonly StyledProperty<int> PixelWidthProperty =
            AvaloniaProperty.Register<MapSymbolIconControl, int>(nameof(PixelWidth));
        public static readonly StyledProperty<int> PixelHeightProperty =
            AvaloniaProperty.Register<MapSymbolIconControl, int>(nameof(PixelHeight));

        public int[]? Pixels { get => GetValue(PixelsProperty); set => SetValue(PixelsProperty, value); }
        public int PixelWidth { get => GetValue(PixelWidthProperty); set => SetValue(PixelWidthProperty, value); }
        public int PixelHeight { get => GetValue(PixelHeightProperty); set => SetValue(PixelHeightProperty, value); }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PixelsProperty || change.Property == PixelWidthProperty || change.Property == PixelHeightProperty)
                UpdateBitmap();
        }

        /// <summary>Copies ARGB icon pixels into an Avalonia BGRA bitmap.</summary>
        private void UpdateBitmap()
        {
            bitmap?.Dispose();
            bitmap = null;
            if (Pixels == null || PixelWidth <= 0 || PixelHeight <= 0 || Pixels.Length != PixelWidth * PixelHeight)
                return;

            bitmap = new WriteableBitmap(new PixelSize(PixelWidth, PixelHeight), new Vector(96, 96),
                                         PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            using (ILockedFramebuffer framebuffer = bitmap.Lock())
                Marshal.Copy(Pixels, 0, framebuffer.Address, Pixels.Length);
            InvalidateVisual();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            bitmap?.Dispose();
            bitmap = null;
        }

        public override void Render(DrawingContext context)
        {
            if (bitmap != null)
                context.DrawImage(bitmap, new Rect(0, 0, Bounds.Width, Bounds.Height));
        }
    }
}
