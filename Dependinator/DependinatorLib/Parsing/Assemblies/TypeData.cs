using Mono.Cecil;

namespace Dependinator.Parsing.Assemblies;

record TypeData(TypeDefinition Type, Node Node, bool IsAsyncStateType);
