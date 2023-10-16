using Mono.Cecil;

namespace Dependinator.Model.Parsing.Assemblies;

record TypeData(TypeDefinition Type, Node Node, bool IsAsyncStateType);
