using Dependinator.Core;

namespace Dependinator.UI.Diagrams;

// User-toggled diagram view options, shared by the SVG renderers and the interaction/UI services.
static class ViewOptions
{
    static bool? isEditingEnabledManual;

    public static bool ShowHiddenNodes { get; private set; } = true;
    public static bool IsEditingEnabled => isEditingEnabledManual ?? !Build.IsStandaloneWasm;

    public static void SetShowHiddenNodes(bool show) => ShowHiddenNodes = show;

    public static void SetIsEditingEnabled(bool enabled) => isEditingEnabledManual = enabled;
}
