// The attribute and interface the C# 15 compiler recognizes a union type by. They ship in the .NET 11
// base class library; while the target framework is net10.0 they are declared here instead, which is
// the documented way (the compiler matches them by name). Delete this file when the target framework
// becomes net11.0.
#if !NET11_0_OR_GREATER
namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
internal sealed class UnionAttribute : Attribute;

internal interface IUnion
{
    object? Value { get; }
}
#endif
