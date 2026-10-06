namespace Dependinator.UI.Diagrams;

// Shared layout constants for the floating node/line toolbars.
static class ToolbarPositioning
{
    // Bottom edge (viewport px) of the fixed chrome at the top: the app bar (3px + ~34px) and
    // the breadcrumb under it (42px + ~24px), plus a small gap. Floating toolbars anchored
    // above a node/line near the top of the screen are pushed down to this line so they are
    // never hidden under that chrome.
    public const double ChromeBottom = 72;
}
