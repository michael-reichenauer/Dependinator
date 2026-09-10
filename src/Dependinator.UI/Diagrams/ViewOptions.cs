using Dependinator.Core;

namespace Dependinator.UI.Diagrams;

// User-toggled diagram view options, shared by the SVG renderers and the interaction/UI services.
static class ViewOptions
{
    static bool? isEditingEnabledManual;

    public static bool ShowHiddenNodes { get; private set; } = true;
    public static bool IsEditingEnabled => isEditingEnabledManual ?? !Build.IsStandaloneWasm;

    // Flips the mouse wheel zoom direction. Needed by users with "natural scrolling" enabled
    // (macOS), which inverts the wheel delta in a way the browser cannot detect.
    public static bool InvertScrollZoom { get; private set; } = false;

    public static void SetShowHiddenNodes(bool show) => ShowHiddenNodes = show;

    public static void SetInvertScrollZoom(bool invert) => InvertScrollZoom = invert;

    public static void SetIsEditingEnabled(bool enabled) => isEditingEnabledManual = enabled;
}
