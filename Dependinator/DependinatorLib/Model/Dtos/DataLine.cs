using Dependinator.Diagrams;


namespace Dependinator.Model.Dtos;

internal class DataLine : Equatable<DataLine>, IDataItem
{
    public DataLine(
        DataNodeName source,
        DataNodeName target,
        IReadOnlyList<Pos> points,
        int linkCount)
    {
        Source = source;
        Target = target;
        Points = points;
        LinkCount = linkCount;

        IsEqualWhenSame(Source, Target);
    }


    public DataNodeName Source { get; }
    public DataNodeName Target { get; }
    public IReadOnlyList<Pos> Points { get; }
    public int LinkCount { get; }

    public override string ToString() => $"{Source}->{Target}";
}

