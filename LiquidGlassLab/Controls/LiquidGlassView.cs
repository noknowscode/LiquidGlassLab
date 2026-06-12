using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.UI;

namespace LiquidGlassLab.Controls;

/// <summary>
/// Apple-style "Liquid Glass" surface for Uno Platform Skia targets.
/// Samples the content already painted behind it via a backdrop SaveLayer,
/// then refracts it (SkSL lens displacement map), blurs it, boosts saturation,
/// and finishes with a tint wash and rim highlights.
/// </summary>
public partial class LiquidGlassView : SKCanvasElement
{
    // Created once and reused for the lifetime of the control: the builder's
    // Dispose tears down native state shared with the effect, so per-frame
    // using-blocks cause a use-after-free on the next render.
    private readonly SKRuntimeEffect? _lensEffect;
    private readonly SKRuntimeShaderBuilder? _lensBuilder;

    public LiquidGlassView()
    {
        _lensEffect = SKRuntimeEffect.CreateShader(LensSksl, out _);
        if (_lensEffect is not null)
        {
            _lensBuilder = new SKRuntimeShaderBuilder(_lensEffect);
        }
    }

    public static readonly DependencyProperty GlassCornerRadiusProperty = DependencyProperty.Register(
        nameof(GlassCornerRadius), typeof(double), typeof(LiquidGlassView), new PropertyMetadata(28.0, OnRenderPropertyChanged));

    public double GlassCornerRadius
    {
        get => (double)GetValue(GlassCornerRadiusProperty);
        set => SetValue(GlassCornerRadiusProperty, value);
    }

    /// <summary>Maximum refraction offset, in pixels, applied at the rim.</summary>
    public static readonly DependencyProperty RefractionStrengthProperty = DependencyProperty.Register(
        nameof(RefractionStrength), typeof(double), typeof(LiquidGlassView), new PropertyMetadata(40.0, OnRenderPropertyChanged));

    public double RefractionStrength
    {
        get => (double)GetValue(RefractionStrengthProperty);
        set => SetValue(RefractionStrengthProperty, value);
    }

    /// <summary>Width of the refracting bezel band around the edge, in pixels.</summary>
    public static readonly DependencyProperty BezelWidthProperty = DependencyProperty.Register(
        nameof(BezelWidth), typeof(double), typeof(LiquidGlassView), new PropertyMetadata(24.0, OnRenderPropertyChanged));

    public double BezelWidth
    {
        get => (double)GetValue(BezelWidthProperty);
        set => SetValue(BezelWidthProperty, value);
    }

    public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.Register(
        nameof(BlurRadius), typeof(double), typeof(LiquidGlassView), new PropertyMetadata(6.0, OnRenderPropertyChanged));

    public double BlurRadius
    {
        get => (double)GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    public static readonly DependencyProperty TintColorProperty = DependencyProperty.Register(
        nameof(TintColor), typeof(Color), typeof(LiquidGlassView), new PropertyMetadata(Color.FromArgb(255, 255, 255, 255), OnRenderPropertyChanged));

    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    public static readonly DependencyProperty TintOpacityProperty = DependencyProperty.Register(
        nameof(TintOpacity), typeof(double), typeof(LiquidGlassView), new PropertyMetadata(0.10, OnRenderPropertyChanged));

    public double TintOpacity
    {
        get => (double)GetValue(TintOpacityProperty);
        set => SetValue(TintOpacityProperty, value);
    }

    public static readonly DependencyProperty SaturationBoostProperty = DependencyProperty.Register(
        nameof(SaturationBoost), typeof(double), typeof(LiquidGlassView), new PropertyMetadata(1.35, OnRenderPropertyChanged));

    public double SaturationBoost
    {
        get => (double)GetValue(SaturationBoostProperty);
        set => SetValue(SaturationBoostProperty, value);
    }

    private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((LiquidGlassView)d).Invalidate();

    protected override void RenderOverride(SKCanvas canvas, Windows.Foundation.Size area)
    {
        var w = (float)area.Width;
        var h = (float)area.Height;
        if (w <= 1 || h <= 1)
        {
            return;
        }

        var radius = (float)Math.Min(GlassCornerRadius, Math.Min(w, h) / 2);
        var bezel = (float)Math.Min(BezelWidth, Math.Min(w, h) / 2);
        var rect = new SKRect(0, 0, w, h);
        using var rrect = new SKRoundRect(rect, radius);

        canvas.Save();
        canvas.ClipRoundRect(rrect, SKClipOperation.Intersect, antialias: true);

        using var backdrop = BuildBackdropFilter(w, h, radius, bezel);
        var rec = new SKCanvasSaveLayerRec
        {
            Bounds = rect,
            Backdrop = backdrop,
        };
        canvas.SaveLayer(in rec);
        canvas.Restore();

        DrawTint(canvas, rrect);
        DrawRimHighlight(canvas, rect, radius);

        canvas.Restore();
    }

    private SKImageFilter? BuildBackdropFilter(float w, float h, float radius, float bezel)
    {
        SKImageFilter? chain = null;

        var blurSigma = (float)BlurRadius;
        if (blurSigma > 0.05f)
        {
            chain = SKImageFilter.CreateBlur(blurSigma, blurSigma, SKShaderTileMode.Clamp);
        }

        if (_lensBuilder is not null && RefractionStrength > 0.5 && bezel > 0.5f)
        {
            _lensBuilder.Uniforms["uSize"] = new[] { w, h };
            _lensBuilder.Uniforms["uRadius"] = radius;
            _lensBuilder.Uniforms["uBezel"] = bezel;
            using var lensShader = _lensBuilder.Build();
            if (lensShader is not null)
            {
                var displacement = SKImageFilter.CreateShader(lensShader, dither: false);
                // Offset = scale * (channel - 0.5); the shader encodes a [-1, 1] normal into [0, 1],
                // so scale = 2 * desired max pixel offset.
                var refracted = SKImageFilter.CreateDisplacementMapEffect(
                    SKColorChannel.R,
                    SKColorChannel.G,
                    (float)RefractionStrength * 2f,
                    displacement,
                    chain);
                chain?.Dispose();
                displacement.Dispose();
                chain = refracted;
            }
        }

        var saturation = (float)SaturationBoost;
        if (Math.Abs(saturation - 1f) > 0.01f)
        {
            using var colorFilter = SKColorFilter.CreateColorMatrix(SaturationMatrix(saturation));
            var saturated = SKImageFilter.CreateColorFilter(colorFilter, chain);
            chain?.Dispose();
            chain = saturated;
        }

        return chain;
    }

    private void DrawTint(SKCanvas canvas, SKRoundRect rrect)
    {
        var tint = TintColor;
        var alpha = (byte)Math.Clamp(TintOpacity * 255, 0, 255);
        if (alpha == 0)
        {
            return;
        }

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = new SKColor(tint.R, tint.G, tint.B, alpha),
        };
        canvas.DrawRoundRect(rrect, paint);
    }

    private static void DrawRimHighlight(SKCanvas canvas, SKRect rect, float radius)
    {
        var inset = rect;
        inset.Inflate(-0.75f, -0.75f);
        using var rim = new SKRoundRect(inset, Math.Max(radius - 0.75f, 0));

        // Bright rim catching light from the top-left.
        using var topGradient = SKShader.CreateLinearGradient(
            new SKPoint(rect.Left, rect.Top),
            new SKPoint(rect.Left, rect.Bottom),
            new[]
            {
                SKColors.White.WithAlpha(200),
                SKColors.White.WithAlpha(30),
                SKColors.White.WithAlpha(110),
            },
            new[] { 0f, 0.55f, 1f },
            SKShaderTileMode.Clamp);

        using var rimPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Shader = topGradient,
        };
        canvas.DrawRoundRect(rim, rimPaint);

        // Soft inner glow just inside the rim.
        var glowRect = rect;
        glowRect.Inflate(-2.5f, -2.5f);
        using var glow = new SKRoundRect(glowRect, Math.Max(radius - 2.5f, 0));
        using var glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            Color = SKColors.White.WithAlpha(28),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2.5f),
        };
        canvas.DrawRoundRect(glow, glowPaint);
    }

    private static float[] SaturationMatrix(float s)
    {
        // Rec. 709 luma weights.
        const float lr = 0.2126f, lg = 0.7152f, lb = 0.0722f;
        return new[]
        {
            lr + (1 - lr) * s, lg - lg * s,       lb - lb * s,       0, 0,
            lr - lr * s,       lg + (1 - lg) * s, lb - lb * s,       0, 0,
            lr - lr * s,       lg - lg * s,       lb + (1 - lb) * s, 0, 0,
            0,                 0,                 0,                 1, 0,
        };
    }

    /// <summary>
    /// Lens displacement field: a rounded-rect SDF drives an outward normal whose
    /// magnitude ramps up across the bezel band, encoded RG as 0.5 + n * 0.5.
    /// The displacement filter then samples the backdrop offset along that normal,
    /// which reads as edge refraction, like a slab of glass with curved edges.
    /// </summary>
    private const string LensSksl = """
        uniform float2 uSize;
        uniform float uRadius;
        uniform float uBezel;

        float sdRoundRect(float2 p, float2 b, float r) {
            float2 q = abs(p) - b + r;
            return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
        }

        half4 main(float2 coord) {
            float2 half_ = uSize * 0.5;
            float2 p = coord - half_;
            float d = sdRoundRect(p, half_, uRadius);

            // 0 at the inner edge of the bezel, 1 at the rim.
            float t = clamp(1.0 + d / uBezel, 0.0, 1.0);
            float k = t * t * (3.0 - 2.0 * t);

            float eps = 1.0;
            float gx = sdRoundRect(p + float2(eps, 0.0), half_, uRadius)
                     - sdRoundRect(p - float2(eps, 0.0), half_, uRadius);
            float gy = sdRoundRect(p + float2(0.0, eps), half_, uRadius)
                     - sdRoundRect(p - float2(0.0, eps), half_, uRadius);
            float2 n = float2(gx, gy) / max(length(float2(gx, gy)), 1e-4);

            // Sample toward the outside at the rim: content compresses at the
            // edge and the surroundings bend into view, like a convex lens.
            float2 disp = n * k;
            return half4(0.5 + disp.x * 0.5, 0.5 + disp.y * 0.5, 0.0, 1.0);
        }
        """;
}
