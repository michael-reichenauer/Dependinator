

namespace Dependinator.Models;

class ModelEditCommand : ICommand
{
    double? Zoom { get; set; }
    double? ZoomCopy { get; set; }
    Pos? Offset { get; set; }
    Pos? OffsetCopy { get; set; }

    public void Execute(IModel model)
    {
        if (Zoom != null) (ZoomCopy, model.Zoom) = (model.Zoom, (double)Zoom);
        if (Offset != null) (OffsetCopy, model.Offset) = (model.Offset, Offset);
    }

    public void Unexecute(IModel model)
    {
        if (ZoomCopy != null) (Zoom, model.Zoom) = (model.Zoom, (double)ZoomCopy);
        if (OffsetCopy != null) (Offset, model.Offset) = (model.Offset, OffsetCopy);
    }
}
