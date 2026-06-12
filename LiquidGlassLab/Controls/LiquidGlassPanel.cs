using Windows.UI;

namespace LiquidGlassLab.Controls;

/// <summary>
/// Content host with a Liquid Glass surface behind it. Wraps
/// <see cref="LiquidGlassView"/> so consumers write one tag with content
/// instead of hand-composing a Grid; glass parameters are forwarded to the
/// effect layer through the default template.
/// </summary>
public partial class LiquidGlassPanel : ContentControl
{
    public LiquidGlassPanel()
    {
        DefaultStyleKey = typeof(LiquidGlassPanel);
    }

    public static readonly DependencyProperty GlassCornerRadiusProperty = DependencyProperty.Register(
        nameof(GlassCornerRadius), typeof(double), typeof(LiquidGlassPanel), new PropertyMetadata(28.0));

    public double GlassCornerRadius
    {
        get => (double)GetValue(GlassCornerRadiusProperty);
        set => SetValue(GlassCornerRadiusProperty, value);
    }

    public static readonly DependencyProperty RefractionStrengthProperty = DependencyProperty.Register(
        nameof(RefractionStrength), typeof(double), typeof(LiquidGlassPanel), new PropertyMetadata(40.0));

    public double RefractionStrength
    {
        get => (double)GetValue(RefractionStrengthProperty);
        set => SetValue(RefractionStrengthProperty, value);
    }

    public static readonly DependencyProperty BezelWidthProperty = DependencyProperty.Register(
        nameof(BezelWidth), typeof(double), typeof(LiquidGlassPanel), new PropertyMetadata(24.0));

    public double BezelWidth
    {
        get => (double)GetValue(BezelWidthProperty);
        set => SetValue(BezelWidthProperty, value);
    }

    public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.Register(
        nameof(BlurRadius), typeof(double), typeof(LiquidGlassPanel), new PropertyMetadata(6.0));

    public double BlurRadius
    {
        get => (double)GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    public static readonly DependencyProperty ChromaticAberrationProperty = DependencyProperty.Register(
        nameof(ChromaticAberration), typeof(double), typeof(LiquidGlassPanel), new PropertyMetadata(0.4));

    public double ChromaticAberration
    {
        get => (double)GetValue(ChromaticAberrationProperty);
        set => SetValue(ChromaticAberrationProperty, value);
    }

    public static readonly DependencyProperty TintColorProperty = DependencyProperty.Register(
        nameof(TintColor), typeof(Color), typeof(LiquidGlassPanel), new PropertyMetadata(Color.FromArgb(255, 255, 255, 255)));

    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    public static readonly DependencyProperty TintOpacityProperty = DependencyProperty.Register(
        nameof(TintOpacity), typeof(double), typeof(LiquidGlassPanel), new PropertyMetadata(0.10));

    public double TintOpacity
    {
        get => (double)GetValue(TintOpacityProperty);
        set => SetValue(TintOpacityProperty, value);
    }

    public static readonly DependencyProperty SaturationBoostProperty = DependencyProperty.Register(
        nameof(SaturationBoost), typeof(double), typeof(LiquidGlassPanel), new PropertyMetadata(1.35));

    public double SaturationBoost
    {
        get => (double)GetValue(SaturationBoostProperty);
        set => SetValue(SaturationBoostProperty, value);
    }
}
