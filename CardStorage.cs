using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace ScheduleCard;

internal sealed record CardPlacement(double Left, double Top, double Width, double Height, bool Topmost = false);

internal sealed class CardStorage(string directory)
{
    private string PlacementPath => Path.Combine(directory, "placement.json");
    internal string ImagePath => Path.Combine(directory, "current.png");

    internal CardPlacement? ReadPlacement()
    {
        try
        {
            using var input = File.OpenRead(PlacementPath);
            return JsonSerializer.Deserialize<CardPlacement>(input);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    internal void SavePlacement(CardPlacement placement) =>
        WriteAtomically(PlacementPath, output => JsonSerializer.Serialize(output, placement));

    internal BitmapSource ReadImage(string path)
    {
        using var input = File.OpenRead(path);
        BitmapSource bitmap = BitmapFrame.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        bitmap.Freeze();
        return bitmap;
    }

    internal BitmapSource ReplaceImage(string path) => ReplaceImage(ReadImage(path));

    internal BitmapSource ReplaceImage(BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        WriteAtomically(ImagePath, encoder.Save);
        return bitmap;
    }

    private void WriteAtomically(string path, Action<Stream> write)
    {
        Directory.CreateDirectory(directory);
        var staging = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(output);
                output.Flush(flushToDisk: true);
            }
            File.Move(staging, path, overwrite: true);
        }
        finally
        {
            File.Delete(staging);
        }
    }
}
