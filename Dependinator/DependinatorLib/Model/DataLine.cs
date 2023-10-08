using Dependinator.Diagrams;

namespace Dependinator.Model;

record DataLine(
    DataNodeName Source,
    DataNodeName Target,
    IReadOnlyList<Pos> Points,
    int LinkCount)
    : IDataItem
{
    public override string ToString() => $"{Source}->{Target}";
}

