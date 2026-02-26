using Mono.Cecil;

namespace Dependinator.Core.Parsing.Assemblies;

record TypeData(TypeDefinition Type, Node Node, bool IsAsyncStateType);
