namespace Dependinator.Modeling.Dtos;

[Serializable]
record FileSpanDto(string Path, int StarLine, int EndLine);
