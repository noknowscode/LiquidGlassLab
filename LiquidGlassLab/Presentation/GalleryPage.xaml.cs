using Microsoft.UI.Xaml.Media.Animation;

namespace LiquidGlassLab.Presentation;

public sealed partial class GalleryPage : Page
{
    public GalleryPage()
    {
        this.InitializeComponent();
        Loaded += (_, _) => StartBlobDrift();
    }

    private void StartBlobDrift()
    {
        // Slow background drift proves the glass resolves its backdrop live.
        var storyboard = new Storyboard();
        storyboard.Children.Add(Drift(Blob1Transform, "X", 120, 9));
        storyboard.Children.Add(Drift(Blob1Transform, "Y", 70, 7));
        storyboard.Children.Add(Drift(Blob2Transform, "X", -140, 11));
        storyboard.Children.Add(Drift(Blob2Transform, "Y", -90, 8));
        storyboard.Begin();
    }

    private static DoubleAnimation Drift(DependencyObject target, string property, double to, double seconds)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = new Duration(TimeSpan.FromSeconds(seconds)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }
}
