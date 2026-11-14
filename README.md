# ScheduleCard · 课表卡片

一个用于显示课表截图的 Windows 小窗口。换课表时，把新图片拖进卡片即可。

## 下载和使用

1. 下载并解压 [ScheduleCard.zip](https://github.com/femiyamshiro/schedule-card/raw/refs/heads/main/downloads/ScheduleCard.zip)。
2. 如果系统缺少运行时，安装 [.NET 8 Desktop Runtime（Windows x64）](https://dotnet.microsoft.com/download/dotnet/8.0)。
3. 双击 `ScheduleCard.exe`。把图片从资源管理器拖进卡片，或点击卡片后按 `Ctrl+V` 粘贴截图。也可以用「换图」选择文件。
4. 点击图片可放大。拖动标题栏移动卡片，拖动任意一条边或一个角调整大小。
5. 点右上角「置顶」让卡片保持在其他普通窗口前面。再次点击「已置顶」即可关闭。

应用每次显示一张图片。新图会替换卡片里的旧图，源文件保留在原处。图片副本、卡片位置、尺寸和置顶状态保存在本机 `%LOCALAPPDATA%\ScheduleCard`，重开时会恢复。

卡片以独立窗口运行。再次打开 `ScheduleCard.exe` 可以尝试唤回当前卡片。

## 从源码构建

在 Windows 上使用 .NET 8 SDK 或兼容的更高版本。

```powershell
dotnet build .\ScheduleCard.csproj -c Release
```

入口为 `bin\Release\net8.0-windows\ScheduleCard.exe`。
