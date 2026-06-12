using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.UI;

namespace LiquidGlassLab.Controls;

/// <summary>
/// Apple-style "Liquid Glass" surface for Uno Platform Skia targets.
/// Samples the content already painted behind it via a backdrop SaveLayer,
/// then refracts it (SkSL lens displacement map, with per-channel chromatic
/// aberration), blurs it, boosts saturation, and finishes with a tint wash,
/// rim highlights, and an inner bottom shade.
///
/// Threading model: dependency properties are snapshotted to plain fields on
/// the UI thread; RenderOverride (render thread) reads only those fields and
/// rebuilds its cached Skia objects when they drift from the built-with values.
/// </summary>
public partial class LiquidGlassView : SKCanvasElement
{
    // Created once and reused for the lifetime of the control: the builder's
    // Dispose tears down native state shared with the effect, so per-frame
    // using-blocks cause a use-after-free on the next render.
    private readonly SKRuntimeEffect? _lensEffect;
    private readonly SKRuntimeShaderBuilder? _lensBuilder;

    // UI-thread snapshots of the dependency properties.
    private float _radius = 28f;
    private float _strength = 40f;
    private float _bezel = 24f;
    private float _blur = 6f;
    private float _aberration = 0.4f;
    private float _saturation = 1.35f;
    private SKColor _tint = new(255, 255, 255, 26);

    // Render-thread caches and the values they were built with.
    private SKImageFilter? _backdropFilter;
    private float _builtW, _builtH, _builtRadius, _builtBezel, _builtBlur, _builtStrength, _builtAberration, _builtSaturation;
    private SKPaint? _tintPaint;
    private SKColor _builtTint;
    private SKPaint? _rimPaint;
    private SKPaint? _glowPaint;
    private SKPaint? _shadePaint;
    private float _chromeW, _chromeH;

    public LiquidGlassView()
    {
        _lensEffect = SKRuntimeEffect.CreateShader(LensSksl, out _);
        if (_lensEffect is not null)
        {
            _lensBuilder = new SKRuntimeShaderBuilder(_lensEffect);
        }

        Unloaded += (_, _) => DisposeRenderCaches();
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

    /// <summary>0..1, splits the refraction per color channel at the rim.</summary>
    public static readonly DependencyProperty ChromaticAberrationProperty = DependencyProperty.Register(
        nameof(ChromaticAberration), typeof(double), typeof(LiquidGlassView), new PropertyMetadata(0.4, OnRenderPropertyChanged));

    public double ChromaticAberration
    {
        get => (double)GetValue(ChromaticAberrationProperty);
        set => SetValue(ChromaticAberrationProperty, value);
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
    {
        var view = (LiquidGlassView)d;
        view.SnapshotProperties();
        view.Invalidate();
    }

    private void SnapshotProperties()
    {
        _radius = (float)GlassCornerRadius;
        _strength = (float)RefractionStrength;
        _bezel = (float)BezelWidth;
        _blur = (float)BlurRadius;
        _aberration = (float)Math.Clamp(ChromaticAberration, 0, 1);
        _saturation = (float)SaturationBoost;
        var tint = TintColor;
        _tint = new SKColor(tint.R, tint.G, tint.B, (byte)Math.Clamp(TintOpacity * 255, 0, 255));
    }

    protected override void RenderOverride(SKCanvas canvas, Windows.Foundation.Size area)
    {
        var w = (float)area.Width;
        var h = (float)area.Height;
        if (w <= 1 || h <= 1)
        {
            return;
        }

        var radius = Math.Min(_radius, Math.Min(w, h) / 2);
        var bezel = Math.Min(_bezel, Math.Min(w, h) / 2);
        var rect = new SKRect(0, 0, w, h);
        using var rrect = new SKRoundRect(rect, radius);

        EnsureBackdropFilter(w, h, radius, bezel);
        EnsureChrome(rect, radius);

        canvas.Save();
        canvas.ClipRoundRect(rrect, SKClipOperation.Intersect, antialias: true);

        var rec = new SKCanvasSaveLayerRec
        {
            Bounds = rect,
            Backdrop = _backdropFilter,
        };
        canvas.SaveLayer(in rec);
        canvas.Restore();

        if (_tintPaint is not null && _tint.Alpha > 0)
        {
            canvas.DrawRoundRect(rrect, _tintPaint);
        }

        if (_shadePaint is not null)
        {
            canvas.DrawRoundRect(rrect, _shadePaint);
        }

        var inset = rect;
        inset.Inflate(-0.75f, -0.75f);
        using var rim = new SKRoundRect(inset, Math.Max(radius - 0.75f, 0));
        canvas.DrawRoundRect(rim, _rimPaint);

        var glowRect = rect;
        glowRect.Inflate(-2.5f, -2.5f);
        using var glow = new SKRoundRect(glowRect, Math.Max(radius - 2.5f, 0));
        canvas.DrawRoundRect(glow, _glowPaint);

        canvas.Restore();
    }

    private void EnsureBackdropFilter(float w, float h, float radius, float bezel)
    {
        if (_backdropFilter is not null
            && _builtW == w && _builtH == h && _builtRadius == radius && _builtBezel == bezel
            && _builtBlur == _blur && _builtStrength == _strength
            && _builtAberration == _aberration && _builtSaturation == _saturation)
        {
            return;
        }

        _backdropFilter?.Dispose();
        _backdropFilter = BuildBackdropFilter(w, h, radius, bezel);
        (_builtW, _builtH, _builtRadius, _builtBezel) = (w, h, radius, bezel);
        (_builtBlur, _builtStrength, _builtAberration, _builtSaturation) = (_blur, _strength, _aberration, _saturation);
    }

    private SKImageFilter? BuildBackdropFilter(float w, float h, float radius, float bezel)
    {
        SKImageFilter? chain = null;

        if (_blur > 0.05f)
        {
            chain = SKImageFilter.CreateBlur(_blur, _blur, SKShaderTileMode.Clamp);
        }

        if (_lensBuilder is not null && _strength > 0.5f && bezel > 0.5f)
        {
            _lensBuilder.Uniforms["uSize"] = new[] { w, h };
            _lensBuilder.Uniforms["uRadius"] = radius;
            _lensBuilder.Uniforms["uBezel"] = bezel;
            using var lensShader = _lensBuilder.Build();
            if (lensShader is not null)
            {
                using var displacement = SKImageFilter.CreateShader(lensShader, dither: false);
                var refracted = _aberration > 0.01f
                    ? BuildChromaticRefraction(displacement, chain)
                    : SKImageFilter.CreateDisplacementMapEffect(
                        SKColorChannel.R, SKColorChannel.G, _strength * 2f, displacement, chain);
                chain?.Dispose();
                chain = refracted;
            }
        }

        if (Math.Abs(_saturation - 1f) > 0.01f)
        {
            using var colorFilter = SKColorFilter.CreateColorMatrix(SaturationMatrix(_saturation));
            var saturated = SKImageFilter.CreateColorFilter(colorFilter, chain);
            chain?.Dispose();
            chain = saturated;
        }

        return chain;
    }

    /// <summary>
    /// Splits the displacement per color channel — red bends more, blue less —
    /// then recombines additively. Shared filter nodes (blur input, lens shader)
    /// are evaluated once per frame by Skia's filter cache, so this costs three
    /// displacement passes over the glass area, not three full chains.
    /// </summary>
    private SKImageFilter BuildChromaticRefraction(SKImageFilter displacement, SKImageFilter? input)
    {
        var spread = 0.35f * _aberration;

        SKImageFilter Channel(float scale, float[] matrix)
        {
            using var displaced = SKImageFilter.CreateDisplacementMapEffect(
                SKColorChannel.R, SKColorChannel.G, _strength * 2f * scale, displacement, input);
            using var isolate = SKColorFilter.CreateColorMatrix(matrix);
            return SKImageFilter.CreateColorFilter(isolate, displaced);
        }

        using var r = Channel(1f + spread, ChannelMatrix(0));
        using var g = Channel(1f, ChannelMatrix(1));
        using var b = Channel(1f - spread, ChannelMatrix(2));

        using var rg = SKImageFilter.CreateArithmetic(0, 1, 1, 0, enforcePMColor: true, background: r, foreground: g);
        return SKImageFilter.CreateArithmetic(0, 1, 1, 0, enforcePMColor: true, background: rg, foreground: b);
    }

    private static float[] ChannelMatrix(int channel)
    {
        var m = new float[20];
        m[channel * 5 + channel] = 1; // keep that channel
        m[18] = 1;                    // keep alpha
        return m;
    }

    private void EnsureChrome(SKRect rect, float radius)
    {
        if (_tintPaint is not null && _builtTint == _tint && _chromeW == rect.Width && _chromeH == rect.Height)
        {
            return;
        }

        DisposeChrome();
        _chromeW = rect.Width;
        _chromeH = rect.Height;
        _builtTint = _tint;

        _tintPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Color = _tint,
        };

        // Bright rim catching light from the top, dimmer return along the bottom.
        _rimPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top),
                new SKPoint(rect.Left, rect.Bottom),
                new[]
                {
                    SKColors.White.WithAlpha(200),
                    SKColors.White.WithAlpha(30),
                    SKColors.White.WithAlpha(110),
                },
                new[] { 0f, 0.55f, 1f },
                SKShaderTileMode.Clamp),
        };

        // Soft glow just inside the rim.
        _glowPaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            Color = SKColors.White.WithAlpha(28),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2.5f),
        };

        // Faint shade pooling at the bottom of the slab for depth.
        _shadePaint = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Fill,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(rect.Left, rect.Top + rect.Height * 0.6f),
                new SKPoint(rect.Left, rect.Bottom),
                new[] { SKColors.Black.WithAlpha(0), SKColors.Black.WithAlpha(26) },
                null,
                SKShaderTileMode.Clamp),
        };
    }

    private void DisposeChrome()
    {
        _tintPaint?.Dispose();
        _rimPaint?.Dispose();
        _glowPaint?.Dispose();
        _shadePaint?.Dispose();
        _tintPaint = _rimPaint = _glowPaint = _shadePaint = null;
    }

    private void DisposeRenderCaches()
    {
        _backdropFilter?.Dispose();
        _backdropFilter = null;
        _builtW = 0;
        DisposeChrome();
        _chromeW = 0;
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
