using System.Globalization;
using System.Web;
using Dependinator.Core.Shared;
using Dependinator.UI.Diagrams.Interaction;
using Dependinator.UI.Modeling.Models;
using Dependinator.UI.Shared.CloudSync;
using Dependinator.UI.Shared.Types;
using Dependinator.UI.Shared.VsCode;
using Microsoft.AspNetCore.Components;
using Shared;

namespace Dependinator.UI.Shared;

// A view worth linking to: the canvas point at the viewport's center and the zoom.
readonly record struct ViewLink(double CenterX, double CenterY, double Zoom);

// What a share link points at: a model (by key), and optionally a node to show or a view to
// restore. A link without a model applies to whatever model is open.
record ShareLinkTarget(string? ModelKey, string? NodeName, ViewLink? View);

// The link format: https://dependinator.com/?m=<model key>&n=<node name>&v=<x>,<y>,<zoom>.
// The model key is the cloud sync key of the model's path (so the same model has the same key
// on every device), or "demo" for the built-in demo model. Node names are stable across
// parses, which ids (hashes) also are, but names make links readable.
static class ShareLink
{
    public const string DemoKey = "demo";

    public static string ModelKeyFor(string modelPath) =>
        modelPath == DemoModel.Path ? DemoKey : CloudModelPath.CreateKey(modelPath);

    public static string Build(string baseUrl, string modelKey, string? nodeName, ViewLink? view)
    {
        var query = new List<string> { $"m={Uri.EscapeDataString(modelKey)}" };
        if (nodeName is not null)
            query.Add($"n={Uri.EscapeDataString(nodeName)}");
        if (view is { } v)
            query.Add($"v={Format(v.CenterX)},{Format(v.CenterY)},{Format(v.Zoom)}");
        return $"{baseUrl.TrimEnd('/')}/?{string.Join("&", query)}";
    }

    // The target in an address, or null when it carries none of the link parameters.
    public static ShareLinkTarget? Parse(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || string.IsNullOrEmpty(parsed.Query))
            return null;
        var query = HttpUtility.ParseQueryString(parsed.Query);
        var modelKey = Clean(query["m"]);
        var nodeName = Clean(query["n"]);
        var view = ParseView(query["v"]);
        if (modelKey is null && nodeName is null && view is null)
            return null;
        return new ShareLinkTarget(modelKey, nodeName, view);
    }

    static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static ViewLink? ParseView(string? value)
    {
        if (value is null)
            return null;
        var parts = value.Split(',');
        if (parts.Length != 3)
            return null;
        if (
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var zoom)
            || !double.IsFinite(x)
            || !double.IsFinite(y)
            || zoom <= 0
            || !double.IsFinite(zoom)
        )
            return null;
        return new ViewLink(x, y, zoom);
    }

    // Nine significant digits: the zoom shrinks eightfold per container level, so a fixed
    // number of decimals would round a deep view's zoom away (and its center to whole pixels).
    static string Format(double value) => value.ToString("G9", CultureInfo.InvariantCulture);
}

// Share links: copies a link to a node or to the current view, and, when the app was opened
// with such a link, resolves its model (demo, a model on this device, or one in the account's
// cloud) and shows the node or view once the model is loaded. Links always point at the web
// app, so from VS Code they open in a browser and need the model to be synced to the cloud.
interface IShareLinkService
{
    // The link the app was opened with, if any (taken once by the startup code).
    ShareLinkTarget? TakeStartupTarget();

    Task CopyNodeLinkAsync(NodeId nodeId);
    Task CopyViewLinkAsync();

    // The local path of the link's model when it is the demo or on this device; the cloud copy
    // when it is only in the account's cloud; an error when neither.
    Task<Result<LinkedModel>> ResolveModelAsync(string modelKey);

    // Shows the link's node or view in the loaded model.
    Task ApplyAsync(ShareLinkTarget target);
}

// Where a linked model was found.
record LinkedModel(string? LocalPath, CloudModelMetadata? CloudModel);

[Scoped]
class ShareLinkService(
    NavigationManager navigationManager,
    IModelMgr modelMgr,
    IModelListService modelListService,
    IAppCloudSyncService appCloudSyncService,
    IPanZoomService panZoomService,
    INavigationService navigationService,
    IScreenService screenService,
    IJSInterop jsInterop,
    IVsCodeSendService vsCodeSendService,
    IApplicationEvents applicationEvents
) : IShareLinkService
{
    // Links from the VS Code extension open in a browser, on the web app.
    const string WebAppUrl = "https://dependinator.com/";

    // How long the startup waits for the cloud sign-in and the account's model list before
    // giving up on a link to a cloud-only model.
    static readonly TimeSpan CloudReadyTimeout = TimeSpan.FromSeconds(10);

    bool isStartupTargetTaken;

    public ShareLinkTarget? TakeStartupTarget()
    {
        if (isStartupTargetTaken || Dependinator.Core.Build.IsVsCodeExtWasm)
            return null;
        isStartupTargetTaken = true;
        return ShareLink.Parse(navigationManager.Uri);
    }

    public async Task CopyNodeLinkAsync(NodeId nodeId)
    {
        string? nodeName;
        string modelPath;
        using (var model = modelMgr.UseModel())
        {
            modelPath = model.Path;
            nodeName = model.Nodes.TryGetValue(nodeId, out var node) ? node.Name : null;
        }
        if (nodeName is null || string.IsNullOrEmpty(modelPath))
            return;
        await CopyAsync(ShareLink.Build(BaseUrl, ShareLink.ModelKeyFor(modelPath), nodeName, null), modelPath);
    }

    public async Task CopyViewLinkAsync()
    {
        string modelPath;
        ViewLink view;
        using (var model = modelMgr.UseModel())
        {
            modelPath = model.Path;
            var svgRect = screenService.SvgRect;
            var center = new Pos(
                model.Offset.X + svgRect.Width / 2 * model.Zoom,
                model.Offset.Y + svgRect.Height / 2 * model.Zoom
            );
            view = new ViewLink(center.X, center.Y, model.Zoom);
        }
        if (string.IsNullOrEmpty(modelPath) || view.Zoom <= 0)
            return;
        await CopyAsync(ShareLink.Build(BaseUrl, ShareLink.ModelKeyFor(modelPath), null, view), modelPath);
    }

    string BaseUrl => Dependinator.Core.Build.IsVsCodeExtWasm ? WebAppUrl : navigationManager.BaseUri;

    async Task CopyAsync(string link, string modelPath)
    {
        var isCopied = Dependinator.Core.Build.IsVsCodeExtWasm
            ? await vsCodeSendService.CopyToClipboardAsync(link)
            : await jsInterop.Call<bool>("copyToClipboard", link);
        if (!isCopied)
        {
            applicationEvents.TriggerErrorReported($"Could not copy the link. It is: {link}");
            return;
        }

        // The link opens on the web app; a model gets there by being synced to the cloud.
        var isReachable = modelPath == DemoModel.Path || appCloudSyncService.SyncState?.Baseline is not null;
        applicationEvents.TriggerInfoReported(
            isReachable
                ? "Link copied. It opens on dependinator.com for an account that has this model synced."
                : "Link copied, but this model is not synced to the cloud yet, so the link cannot open it "
                    + "elsewhere. Enable device sync (the cloud button) first."
        );
    }

    public async Task<Result<LinkedModel>> ResolveModelAsync(string modelKey)
    {
        if (modelKey == ShareLink.DemoKey)
            return new LinkedModel(DemoModel.Path, null);

        var local = modelListService
            .GetModelItems()
            .FirstOrDefault(item => item.IsLocal && ShareLink.ModelKeyFor(item.Path) == modelKey);
        if (local is not null)
            return new LinkedModel(local.Path, null);

        await WaitForCloudAsync();
        var cloud = appCloudSyncService.CloudModels.FirstOrDefault(cm =>
            string.Equals(cm.ModelKey, modelKey, StringComparison.OrdinalIgnoreCase)
        );
        if (cloud is not null)
            return new LinkedModel(null, cloud);

        return new Error(
            appCloudSyncService.AuthState.IsAuthenticated
                ? "The linked model is not on this device or in this account's cloud."
                : "The linked model is not on this device. Sign in (the cloud button) with the account that synced it, then open the link again."
        );
    }

    // The sign-in state and then the cloud model list arrive shortly after startup; the list is
    // what the link needs (IsConnecting alone turns false a network round trip too early).
    async Task WaitForCloudAsync()
    {
        var deadline = DateTime.UtcNow + CloudReadyTimeout;
        while (!appCloudSyncService.HasLoadedCloudModels && DateTime.UtcNow < deadline)
            await Task.Delay(100);
    }

    public async Task ApplyAsync(ShareLinkTarget target)
    {
        if (target.NodeName is { } nodeName)
        {
            NodeId? nodeId;
            using (var model = modelMgr.UseModel())
            {
                nodeId = model.Nodes.Values.FirstOrDefault(n => n.Name == nodeName)?.Id;
            }
            if (nodeId is null)
            {
                applicationEvents.TriggerInfoReported(
                    "The linked node is not in this model (it may have been renamed or removed)."
                );
                return;
            }
            await navigationService.ShowNodeAsync(nodeId);
            return;
        }

        if (target.View is { } view)
            await panZoomService.PanZoomToAsync(new Pos(view.CenterX, view.CenterY), view.Zoom);
    }
}
