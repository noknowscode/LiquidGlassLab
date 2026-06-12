using Microsoft.UI.Xaml.Media;

namespace LiquidGlassLab.Models;

public sealed class FeedItem
{
    public required string Icon { get; init; }
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required Brush Fill { get; init; }
}
