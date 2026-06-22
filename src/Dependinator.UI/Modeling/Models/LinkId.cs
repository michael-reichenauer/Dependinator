using Dependinator.UI.Shared.Types;

namespace Dependinator.UI.Modeling.Models;

public record LinkId(string sourceName, string targetName) : Id(Id.ToId($"{sourceName}->{targetName}"));
