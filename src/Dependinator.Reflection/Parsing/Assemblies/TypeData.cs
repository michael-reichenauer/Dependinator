using Mono.Cecil;

namespace Dependinator.Reflection.Parsing.Assemblies;

record TypeData(TypeDefinition Type, Node Node, bool IsAsyncStateType);
