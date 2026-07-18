using Dependinator.E2E.Tests.Shared;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Dependinator.E2E.Tests.Ui;

// Exporting a diagram area as an SVG/PNG image (AppBar Export menu, ExportService).
public class ExportTests(ITestOutputHelper output) : E2ETestBase(output)
{
    [E2EFact]
    public async Task ExportCurrentView_ShouldDownloadSvgFile()
    {
        await App.GotoMainPageAsync();

        await (await App.OpenSubMenuItemAsync("menu-export", "menu-export-view")).ClickAsync();
        await Expect(App.Dialog).ToBeVisibleAsync();

        IDownload download = await Page.RunAndWaitForDownloadAsync(() => Page.GetByTestId("export-ok").ClickAsync());

        Assert.EndsWith(".svg", download.SuggestedFilename);
        string svg = await ReadDownloadTextAsync(download);
        Assert.StartsWith("<svg", svg);
        Assert.Contains("<defs>", svg);
        Assert.Contains("</svg>", svg);
    }

    [E2EFact]
    public async Task ExportCurrentView_ShouldDownloadPngFile()
    {
        await App.GotoMainPageAsync();

        await (await App.OpenSubMenuItemAsync("menu-export", "menu-export-view")).ClickAsync();
        await Expect(App.Dialog).ToBeVisibleAsync();
        await Page.GetByTestId("export-format-png").ClickAsync();

        IDownload download = await Page.RunAndWaitForDownloadAsync(() => Page.GetByTestId("export-ok").ClickAsync());

        Assert.EndsWith(".png", download.SuggestedFilename);
        byte[] bytes = await ReadDownloadBytesAsync(download);
        // PNG signature: 0x89 'P' 'N' 'G'.
        Assert.True(bytes.Length > 8);
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], bytes[..4]);
    }

    [E2EFact]
    public async Task ExportSelectedArea_ShouldOpenDialog_AndDownloadSvgFile()
    {
        await App.GotoMainPageAsync();

        await (await App.OpenSubMenuItemAsync("menu-export", "menu-export-area")).ClickAsync();
        await DragOnCanvasAsync();

        await Expect(App.Dialog).ToBeVisibleAsync();
        IDownload download = await Page.RunAndWaitForDownloadAsync(() => Page.GetByTestId("export-ok").ClickAsync());

        Assert.EndsWith(".svg", download.SuggestedFilename);
        string svg = await ReadDownloadTextAsync(download);
        Assert.StartsWith("<svg", svg);
    }

    [E2EFact]
    public async Task ExportSelectedArea_ShouldCancelOnEscape()
    {
        await App.GotoMainPageAsync();

        await (await App.OpenSubMenuItemAsync("menu-export", "menu-export-area")).ClickAsync();
        await Page.Keyboard.PressAsync("Escape");

        // The armed selection is canceled: a drag pans the canvas instead of selecting,
        // and no export dialog opens.
        await DragOnCanvasAsync();
        await Expect(App.Dialog).ToBeHiddenAsync();
    }

    // Drags a rectangle across the middle of the diagram canvas.
    async Task DragOnCanvasAsync()
    {
        var box = await App.Canvas.BoundingBoxAsync();
        Assert.NotNull(box);
        float startX = box.X + box.Width * 0.3f;
        float startY = box.Y + box.Height * 0.3f;
        float endX = box.X + box.Width * 0.7f;
        float endY = box.Y + box.Height * 0.7f;

        await Page.Mouse.MoveAsync(startX, startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(endX, endY, new() { Steps = 5 });
        await Page.Mouse.UpAsync();
    }

    static async Task<string> ReadDownloadTextAsync(IDownload download)
    {
        await using Stream stream = (await download.CreateReadStreamAsync())!;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    static async Task<byte[]> ReadDownloadBytesAsync(IDownload download)
    {
        await using Stream stream = (await download.CreateReadStreamAsync())!;
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        return memory.ToArray();
    }
}
