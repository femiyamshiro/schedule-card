using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ScheduleCard;

public partial class CardWindow : Window
{
    private readonly CardStorage storage;
    private readonly DispatcherTimer savePosition;
    private Window? preview;
    private Rect resizeBounds;
    private Point resizeOrigin;

    public CardWindow(string dataDirectory)
    {
        InitializeComponent();
        storage = new CardStorage(dataDirectory);
        var placement = storage.ReadPlacement();
        SetBounds(placement);
        SetTopmost(placement?.Topmost ?? false);
        savePosition = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        savePosition.Tick += (_, _) => SavePosition();
        LocationChanged += (_, _) => QueuePositionSave();
        SizeChanged += (_, _) => QueuePositionSave();
        Closing += (_, _) => SavePosition();
        if (File.Exists(storage.ImagePath))
            TryChange(() => storage.ReadImage(storage.ImagePath));
    }

    private void SetBounds(CardPlacement? placement)
    {
        Rect work = SystemParameters.WorkArea;
        var desktop = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        Width = Math.Clamp(placement?.Width ?? Width, MinWidth, Math.Max(MinWidth, work.Width));
        Height = Math.Clamp(placement?.Height ?? Height, MinHeight, Math.Max(MinHeight, work.Height));
        var previous = new Rect(placement?.Left ?? 0, placement?.Top ?? 0, Width, Height);
        if (placement is not null && desktop.Contains(previous))
        {
            Left = previous.Left;
            Top = previous.Top;
            return;
        }

        Left = Math.Max(work.Left, work.Right - Width - 24);
        Top = work.Top + 72;
    }

    private void QueuePositionSave()
    {
        savePosition.Stop();
        savePosition.Start();
    }

    private void SavePosition()
    {
        savePosition.Stop();
        try
        {
            storage.SavePlacement(new CardPlacement(Left, Top, Width, Height, Topmost));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ShowStatus("卡片位置暂时无法保存。");
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        NativeWindow.Configure(source.Handle);
        source.AddHook((IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (message == NativeWindow.ShowCardMessage)
            {
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
                Activate();
                handled = true;
            }
            return IntPtr.Zero;
        });
    }

    private void MoveCard(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void StartResize(object sender, DragStartedEventArgs e)
    {
        resizeBounds = new Rect(Left, Top, Width, Height);
        resizeOrigin = PointToScreen(Mouse.GetPosition(this));
    }

    private void ResizeCard(object sender, DragDeltaEventArgs e)
    {
        var screenDelta = PointToScreen(Mouse.GetPosition(this)) - resizeOrigin;
        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        var delta = source.CompositionTarget!.TransformFromDevice.Transform(screenDelta);
        var direction = (string)((Thumb)sender).Tag;
        var bounds = ResizedBounds(resizeBounds, direction, delta, MinWidth, MinHeight);
        Width = bounds.Width;
        Height = bounds.Height;
        Left = bounds.Left;
        Top = bounds.Top;
    }

    internal static Rect ResizedBounds(Rect start, string direction, Vector delta, double minWidth, double minHeight)
    {
        double left = start.Left;
        double right = start.Right;
        double top = start.Top;
        double bottom = start.Bottom;

        if (direction.Contains("Left", StringComparison.Ordinal))
            left = Math.Min(left + delta.X, right - minWidth);
        if (direction.Contains("Right", StringComparison.Ordinal))
            right = Math.Max(right + delta.X, left + minWidth);
        if (direction.Contains("Top", StringComparison.Ordinal))
            top = Math.Min(top + delta.Y, bottom - minHeight);
        if (direction.Contains("Bottom", StringComparison.Ordinal))
            bottom = Math.Max(bottom + delta.Y, top + minHeight);

        return new Rect(new Point(left, top), new Point(right, bottom));
    }

    private void FinishResize(object sender, DragCompletedEventArgs e) => SavePosition();

    private void ToggleTopmost(object sender, RoutedEventArgs e)
    {
        SetTopmost(!Topmost);
        SavePosition();
    }

    private void SetTopmost(bool enabled)
    {
        Topmost = enabled;
        PinButton.Content = enabled ? "已置顶" : "置顶";
        PinButton.Tag = enabled ? "Pinned" : null;
        var action = enabled ? "取消置顶" : "置顶课表卡片";
        PinButton.ToolTip = action;
        System.Windows.Automation.AutomationProperties.SetName(PinButton, action);
    }

    private void PickImage(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择课表图片",
            Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
            ShowImageFile(dialog.FileName);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control)
            return;

        e.Handled = true;
        try
        {
            if (Clipboard.GetDataObject() is { } data && AcceptImageData(data))
                return;
            ShowStatus("剪贴板里没有图片。可以先截图或复制一个图片文件。");
        }
        catch (ExternalException)
        {
            ShowStatus("暂时无法读取剪贴板，请再粘贴一次。");
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var acceptable = e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.Bitmap);
        e.Effects = acceptable && e.AllowedEffects.HasFlag(DragDropEffects.Copy) ? DragDropEffects.Copy : DragDropEffects.None;
        DropArea.BorderBrush = e.Effects == DragDropEffects.Copy ? Brushes.MediumAquamarine : NormalBorder;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e) => DropArea.BorderBrush = NormalBorder;

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropArea.BorderBrush = NormalBorder;
        e.Handled = true;
        if (!AcceptImageData(e.Data))
            ShowStatus("请拖入一张已保存的图片，或复制截图后粘贴。");
    }

    internal bool AcceptImageData(IDataObject data)
    {
        if (data.GetData(DataFormats.FileDrop) is string[] files)
        {
            if (files.Length == 1)
                ShowImageFile(files[0]);
            else
                ShowStatus("一次拖入一张图片即可。");
            return true;
        }

        if (data.GetData(DataFormats.Bitmap) is not BitmapSource bitmap)
            return false;

        TryChange(() => storage.ReplaceImage(bitmap));
        return true;
    }

    private void ShowImageFile(string path) => TryChange(() => storage.ReplaceImage(path));

    private void TryChange(Func<BitmapSource> getImage)
    {
        try
        {
            Picture.Source = getImage();
            EmptyHint.Visibility = Visibility.Collapsed;
            PictureButton.Visibility = Visibility.Visible;
            Status.Visibility = Visibility.Collapsed;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or NotSupportedException or ArgumentException or COMException)
        {
            ShowStatus("无法打开或保存这张图。请试一张 PNG 或 JPG 图片。");
        }
    }

    private void ShowStatus(string message)
    {
        Status.Text = message;
        Status.Visibility = Visibility.Visible;
    }

    private void OpenPreview(object sender, RoutedEventArgs e)
    {
        if (Picture.Source is not ImageSource image)
            return;
        if (preview is not null)
        {
            preview.Activate();
            return;
        }

        var view = new Window
        {
            Owner = this, Title = "课表预览", ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 900, Height = 630, MinWidth = 320, MinHeight = 220,
            MaxWidth = SystemParameters.WorkArea.Width, MaxHeight = SystemParameters.WorkArea.Height,
            Background = (Brush)new BrushConverter().ConvertFromString("#090D17")!,
            Content = new Image { Source = image, Stretch = Stretch.Uniform, Margin = new Thickness(16) }
        };
        view.PreviewKeyDown += (_, key) => { if (key.Key == Key.Escape) view.Close(); };
        view.Closed += (_, _) => preview = null;
        preview = view;
        view.Show();
    }

    private void CloseCard(object sender, RoutedEventArgs e) => Close();

    private static Brush NormalBorder { get; } = (Brush)new BrushConverter().ConvertFromString("#303B50")!;
}
