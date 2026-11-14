using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScheduleCard;

internal static class CardVerification
{
    internal static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var cardData = Path.Combine(directory, "card");
        var storage = new CardStorage(cardData);
        var wideImage = Path.Combine(directory, "wide.png");
        var tallImage = Path.Combine(directory, "tall.png");
        var invalidImage = Path.Combine(directory, "invalid.png");
        WriteSample(wideImage, 320, 160);
        WriteSample(tallImage, 100, 240);
        File.WriteAllText(invalidImage, "not an image");

        var card = new CardWindow(cardData);
        VerifyResizeDirections(card.MinWidth, card.MinHeight);
        card.Left = 40;
        card.Top = 70;
        card.Width = 440;
        Require(card.EmptyHint.Visibility == Visibility.Visible, "Starts empty");

        var files = new DataObject();
        files.SetData(DataFormats.FileDrop, new[] { wideImage });
        Require(card.AcceptImageData(files) && ImageWidth(card) == 320, "Files are accepted from FileDrop");
        Require(card.EmptyHint.Visibility == Visibility.Collapsed && card.PictureButton.Visibility == Visibility.Visible, "The image replaces the empty hint");

        files.SetData(DataFormats.FileDrop, new[] { tallImage });
        card.AcceptImageData(files);
        Require(ImageWidth(card) == 100 && storage.ReadImage(storage.ImagePath).PixelHeight == 240, "New drop replaces previous image");

        files.SetData(DataFormats.FileDrop, new[] { invalidImage });
        card.AcceptImageData(files);
        Require(ImageWidth(card) == 100 && card.Status.Visibility == Visibility.Visible, "Invalid drop preserves the image and reports the error");

        var bitmap = new DataObject(DataFormats.Bitmap, storage.ReadImage(wideImage));
        Require(card.AcceptImageData(bitmap) && ImageWidth(card) == 320, "Clipboard bitmap is accepted");
        Require(!card.Topmost, "New cards start unpinned");
        card.PinButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Require(card.Topmost && Equals(card.PinButton.Content, "已置顶"), "Pin button enables topmost");
        card.Close();

        var restored = new CardWindow(cardData);
        Require(ImageWidth(restored) == 320 && restored.Left == 40 && restored.Top == 70 && restored.Width == 440,
            "Another window restores image and placement");
        Require(restored.Picture.Stretch == Stretch.Uniform, "The image preserves its aspect ratio");
        Require(restored.Topmost && Equals(restored.PinButton.Content, "已置顶"), "Topmost is restored");
        Capture(restored, Path.Combine(directory, "card-preview.png"));
        restored.PinButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        Require(!restored.Topmost && storage.ReadPlacement()?.Topmost == false, "Pin button clears topmost");
        restored.Close();

        File.WriteAllText(Path.Combine(cardData, "placement.json"), "{\"Left\":40,\"Top\":70,\"Width\":440,\"Height\":300}");
        Require(storage.ReadPlacement()?.Topmost == false, "Previous placement files default to unpinned");
        app.Shutdown();
        File.WriteAllText(Path.Combine(directory, "result.txt"), "PASS: empty, drop, replacement, invalid drop, paste, restore, image ratio, four edges, four corners, minimum size, topmost toggle and restore, legacy state, render");
    }

    private static void VerifyResizeDirections(double minWidth, double minHeight)
    {
        var start = new Rect(40, 70, 440, 300);
        var delta = new Vector(30, 20);
        (string Direction, Rect Expected)[] checks =
        [
            ("Left", new Rect(70, 70, 410, 300)),
            ("Right", new Rect(40, 70, 470, 300)),
            ("Top", new Rect(40, 90, 440, 280)),
            ("Bottom", new Rect(40, 70, 440, 320)),
            ("TopLeft", new Rect(70, 90, 410, 280)),
            ("TopRight", new Rect(40, 90, 470, 280)),
            ("BottomLeft", new Rect(70, 70, 410, 320)),
            ("BottomRight", new Rect(40, 70, 470, 320))
        ];
        foreach (var (direction, expected) in checks)
            Require(CardWindow.ResizedBounds(start, direction, delta, minWidth, minHeight) == expected,
                $"Resizing from {direction} anchors the opposite side");

        var minimum = CardWindow.ResizedBounds(start, "TopLeft", new Vector(600, 500), minWidth, minHeight);
        Require(minimum.Width == minWidth && minimum.Height == minHeight && minimum.Right == start.Right && minimum.Bottom == start.Bottom,
            "Minimum size keeps the opposite corner fixed");
    }

    private static int ImageWidth(CardWindow card) => ((BitmapSource)card.Picture.Source).PixelWidth;

    private static void Require(bool condition, string description)
    {
        if (!condition)
            throw new InvalidOperationException(description);
    }

    private static void WriteSample(string path, int width, int height)
    {
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen())
        {
            context.DrawRectangle(Brushes.WhiteSmoke, null, new Rect(0, 0, width, height));
            double cellWidth = width / 5d;
            double cellHeight = height / 5d;
            for (int row = 0; row < 5; row++)
            for (int column = 0; column < 5; column++)
            {
                var fill = (row + column) % 3 == 0 ? Brushes.LightSkyBlue : Brushes.White;
                context.DrawRectangle(fill, new Pen(Brushes.LightSlateGray, 0.5), new Rect(column * cellWidth, row * cellHeight, cellWidth, cellHeight));
            }
        }

        var pixels = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        pixels.Render(drawing);
        WritePng(path, pixels);
    }

    private static void Capture(CardWindow card, string path)
    {
        var size = new Size(card.Width, card.Height);
        var layout = (FrameworkElement)card.Content;
        layout.Measure(size);
        layout.Arrange(new Rect(size));
        layout.UpdateLayout();
        var pixels = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        pixels.Render(layout);
        WritePng(path, pixels);
    }

    private static void WritePng(string path, BitmapSource pixels)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(pixels));
        using var output = File.Create(path);
        encoder.Save(output);
    }
}
