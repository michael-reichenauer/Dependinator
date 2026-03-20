using Dependinator.Shared.Types;

namespace Dependinator.Modeling.Models;

public record LinkId(string sourceName, string targetName) : Id(Id.ToId($"{sourceName}->{targetName}"));
