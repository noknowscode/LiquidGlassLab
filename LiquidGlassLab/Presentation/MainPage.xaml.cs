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
        // No Invalidate needed: the backdrop SaveLayer re-resolves on every
        // window recompose, so the cached drawing stays live as it moves.
        GlassTransform.X += e.Delta.Translation.X;
        GlassTransform.Y += e.Delta.Translation.Y;
    }
}
