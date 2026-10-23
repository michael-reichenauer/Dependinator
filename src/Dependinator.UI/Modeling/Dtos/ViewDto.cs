using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Dtos;

// The view of a model (pan offset, zoom and canvas rect), saved apart from the model so that
// panning and zooming write this small record instead of the whole model. Overrides the view in
// the model when a model is loaded.
[Serializable]
record ViewDto
{
    public double Zoom { get; init; }
    public Pos Offset { get; init; } = Pos.None;
    public Rect ViewRect { get; init; } = Rect.None;

    public ModelDto ApplyTo(ModelDto model) => model with { Zoom = Zoom, Offset = Offset, ViewRect = ViewRect };
}
