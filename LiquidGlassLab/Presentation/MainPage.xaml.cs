using Microsoft.UI.Xaml.Input;

namespace LiquidGlassLab.Presentation;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();
    }

    private void OnGlassDrag(object sender, ManipulationDeltaRoutedEventArgs e)
    {
        GlassTransform.X += e.Delta.Translation.X;
        GlassTransform.Y += e.Delta.Translation.Y;

        // The element's own drawing is cached; the backdrop must re-resolve at
        // the new position, so force a redraw while dragging.
        Glass.Invalidate();
    }
}
