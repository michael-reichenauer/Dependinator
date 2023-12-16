namespace Dependinator.Models;

public record Id(string Value);

public record NodeId(string name) : Id(name);
public record LinkId(string sourceName, string targetName) : Id($"{sourceName}->{targetName}");
public record LineId(string sourceName, string targetName) : Id($"{sourceName}=>{targetName}");

