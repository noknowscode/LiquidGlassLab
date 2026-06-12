using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace LiquidGlassLab.Presentation;

public sealed partial class SecondPage : Page
{
    public IReadOnlyList<FeedItem> Items { get; }

    public SecondPage()
    {
        Items = BuildFeed();
        this.InitializeComponent();
    }

    private static IReadOnlyList<FeedItem> BuildFeed()
    {
        var palette = new (string From, string To)[]
        {
            ("#FF6D00", "#F72585"),
            ("#00E5FF", "#3A0CA3"),
            ("#06D6A0", "#118AB2"),
            ("#FFD60A", "#FF6D00"),
            ("#F72585", "#7209B7"),
            ("#4CC9F0", "#4361EE"),
        };

        var posts = new (string Icon, string Title, string Subtitle)[]
        {
            ("🎧", "Skia Sessions Vol. 4", "New mix from the rendering team — 60 fps guaranteed."),
            ("🏔️", "Trip to the Laurentians", "12 photos · Marie-Claude and 3 others"),
            ("🍜", "Ramen night downtown", "Tonkotsu place on Saint-Laurent finally reopened."),
            ("🚲", "Morning ride along the canal", "32.4 km · 1 h 48 min · 3 personal records"),
            ("🎨", "Liquid Glass design review", "Figma file updated — bezel refraction specs inside."),
            ("📦", "Uno.Sdk 6.5 shipped", "SaveLayerRec backdrop filters now in the box."),
            ("🌌", "Northern lights tonight?", "Kp 6 forecast — best viewing after 23:00."),
            ("☕", "New espresso recipe", "18 g in, 36 g out, 27 seconds. Finally dialed in."),
            ("🎮", "SkSL shader jam results", "Winner rendered an aurora in 280 chars of SkSL."),
            ("📚", "Reading: Real-Time Rendering", "Chapter 5 — and suddenly displacement maps make sense."),
            ("🛶", "Canoe-camping Mauricie", "Booked for the long weekend. Two spots left."),
            ("🎹", "Synth patch share", "That glassy pad sound — patch file in comments."),
            ("🧪", "Backdrop filter benchmarks", "Cached DAG vs per-frame rebuild: 11× fewer allocs."),
            ("🌮", "Taco Tuesday relocated", "New spot near the office — al pastor approved."),
            ("🖼️", "Gallery opening Friday", "Generative art show — three Skia pieces on display."),
            ("⛸️", "Lachine canal skate path", "Surface report: smooth, fast, slightly addictive."),
            ("🔭", "Telescope group buy", "Six people in — need two more for the discount."),
            ("🥐", "Croissant ranking update", "New contender in Mile End takes the #2 spot."),
        };

        var items = new List<FeedItem>(posts.Length);
        for (var i = 0; i < posts.Length; i++)
        {
            var (from, to) = palette[i % palette.Length];
            items.Add(new FeedItem
            {
                Icon = posts[i].Icon,
                Title = posts[i].Title,
                Subtitle = posts[i].Subtitle,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Windows.Foundation.Point(0, 0),
                    EndPoint = new Windows.Foundation.Point(1, 1),
                    GradientStops =
                    {
                        new GradientStop { Color = Parse(from), Offset = 0 },
                        new GradientStop { Color = Parse(to), Offset = 1 },
                    },
                },
            });
        }

        return items;
    }

    private static Color Parse(string hex) =>
        Color.FromArgb(
            255,
            Convert.ToByte(hex.Substring(1, 2), 16),
            Convert.ToByte(hex.Substring(3, 2), 16),
            Convert.ToByte(hex.Substring(5, 2), 16));
}
