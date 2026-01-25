using Mono.Cecil;

namespace DependinatorCore.Parsing.Assemblies;

record TypeData(TypeDefinition Type, Node Node, bool IsAsyncStateType);
