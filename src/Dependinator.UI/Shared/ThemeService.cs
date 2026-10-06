using Dependinator.UI.Modeling;
using Microsoft.JSInterop;

namespace Dependinator.UI.Shared;

enum AppTheme
{
    System,
    Light,
    Dark,
}

// The light/dark theme: the user's choice (or the system/VS Code theme when System), applied
// to the MudBlazor palette (via Main.razor), the diagram palette (DColors) and the host page.
interface IThemeService
{
    AppTheme Theme { get; }
    bool IsDark { get; }
    event Action? Changed;

    Task InitAsync(AppTheme theme);
    Task SetThemeAsync(AppTheme theme);
}

[Scoped]
class ThemeService(
    IJSInterop jsInterop,
    IConfigService configService,
    IModelService modelService,
    IApplicationEvents applicationEvents
) : IThemeService, IDisposable
{
    DotNetObjectReference<ThemeService>? selfReference;
    bool isSystemDark;

    public AppTheme Theme { get; private set; } = AppTheme.System;

    public bool IsDark =>
        Theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => isSystemDark,
        };

    public event Action? Changed;

    public async Task InitAsync(AppTheme theme)
    {
        Theme = theme;
        selfReference = jsInterop.Reference(this);
        // In a browser this is prefers-color-scheme; in the VS Code webview it is the editor
        // theme kind, and both keep reporting changes while the app runs.
        isSystemDark = await jsInterop.Call<bool>("watchSystemTheme", selfReference, nameof(OnSystemThemeChanged));
        await ApplyAsync(isInitial: true);
    }

    [JSInvokable]
    public async Task OnSystemThemeChanged(bool isDark)
    {
        if (isSystemDark == isDark)
            return;
        isSystemDark = isDark;
        if (Theme == AppTheme.System)
            await ApplyAsync(isInitial: false);
    }

    public async Task SetThemeAsync(AppTheme theme)
    {
        if (Theme == theme)
            return;
        Theme = theme;
        await configService.SetAsync(config => config.Theme = theme);
        await ApplyAsync(isInitial: false);
    }

    async Task ApplyAsync(bool isInitial)
    {
        // DColors is process-wide (the SVG renderers are static); one theme per process is fine
        // for the WASM hosts (one user per process) and only matters in the Blazor Server dev host.
        DColors.IsDark = IsDark;
        await jsInterop.Call("applyTheme", IsDark);

        // The SVG tiles bake the palette in, so a switch must rebuild them.
        if (!isInitial)
            modelService.ClearCache();

        Changed?.Invoke();
        applicationEvents.TriggerUIStateChanged();
    }

    public void Dispose() => selfReference?.Dispose();
}
