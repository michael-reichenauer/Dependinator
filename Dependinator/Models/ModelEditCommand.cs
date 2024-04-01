namespace Dependinator.Models;


class ModelEditCommand : Command
{
    public double? Zoom { get; set; }
    public double? ZoomCopy { get; set; }
    public Pos? Offset { get; set; }
    public Pos? OffsetCopy { get; set; }


    public override void Execute(IModel model)
    {
        if (Zoom != null) (ZoomCopy, model.Zoom) = (model.Zoom, (double)Zoom);
        if (Offset != null) (OffsetCopy, model.Offset) = (model.Offset, Offset);
    }


    public override void Unexecute(IModel model)
    {
        if (ZoomCopy != null) (Zoom, model.Zoom, ZoomCopy) = (model.Zoom, (double)ZoomCopy, null);
        if (OffsetCopy != null) (Offset, model.Offset, OffsetCopy) = (model.Offset, OffsetCopy, null);
    }
}
