using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Dependinator.Core.Parsing.Assemblies;

internal class MethodParser(LinkHandler linkHandler)
{
    record MethodBodyNode(string MemberName, MethodDefinition Method, bool IsMoveNext);

    readonly Dictionary<string, TypeDefinition> asyncStates = new Dictionary<string, TypeDefinition>();

    readonly LinkHandler linkHandler = linkHandler;
    readonly List<MethodBodyNode> methodBodyNodes = new List<MethodBodyNode>();

    public int IlCount { get; private set; } = 0;

    public void AddAsyncStateType(TypeData typeData)
    {
        asyncStates[typeData.Type.FullName] = typeData.Type;
    }

    public async Task AddMethodLinksAsync(string memberName, MethodDefinition method)
    {
        if (!method.IsConstructor)
        {
            TypeReference returnType = method.ReturnType;
            if (method.ReturnType != method.DeclaringType)
                await linkHandler.AddLinkToTypeAsync(memberName, returnType);
        }

        await method
            .Parameters.Where(parameter => parameter.ParameterType != method.DeclaringType)
            .Select(parameter => parameter.ParameterType)
            .ForEachAsync(parameterType => linkHandler.AddLinkToTypeAsync(memberName, parameterType));

        methodBodyNodes.Add(new MethodBodyNode(memberName, method, false));
    }

    public void AddMethodBodyLink(string memberName, MethodDefinition method)
    {
        methodBodyNodes.Add(new MethodBodyNode(memberName, method, false));
    }

    public async Task AddAllMethodBodyLinksAsync()
    {
        foreach (var l in methodBodyNodes)
        {
            await Task.Yield();
            await AddMethodBodyLinksAsync(l);
        }
    }

    async Task AddMethodBodyLinksAsync(MethodBodyNode methodBodyNode)
    {
        try
        {
            string memberName = methodBodyNode.MemberName;
            MethodDefinition method = methodBodyNode.Method;

            if (method.DeclaringType.IsInterface || !method.HasBody)
            {
                return;
            }

            MethodBody body = method.Body;

            await body.Variables.ForEachAsync(variable =>
                AddLinkToMethodVariableAsync(method, memberName, variable, methodBodyNode.IsMoveNext)
            );

            foreach (Instruction instruction in body.Instructions)
            {
                IlCount++;
                if (instruction.Operand is MethodReference methodCall)
                {
                    await AddLinkToCallMethodAsync(method, memberName, methodCall);
                }
                else if (instruction.Operand is FieldDefinition field)
                {
                    if (method.DeclaringType != field.FieldType)
                        await linkHandler.AddLinkToTypeAsync(memberName, field.FieldType);

                    await linkHandler.AddLinkToMemberAsync(memberName, field);
                }
            }
        }
        catch (Exception e)
        {
            Log.Exception(e);
        }
    }

    async Task AddLinkToMethodVariableAsync(
        MethodDefinition method,
        string memberName,
        VariableDefinition variable,
        bool isMoveNext
    )
    {
        if (
            !isMoveNext
            && variable.VariableType.IsNested
            && asyncStates.TryGetValue(variable.VariableType.FullName, out TypeDefinition? asyncType)
        )
        {
            // There is a async state type with this name
            await AddAsyncStateLinksAsync(memberName, asyncType);
        }
        if (method.DeclaringType != variable.VariableType)
            await linkHandler.AddLinkToTypeAsync(memberName, variable.VariableType);
    }

    async Task AddAsyncStateLinksAsync(string memberName, TypeDefinition asyncType)
    {
        // Try to get the "MovNext method with contains the actual "async/await" code
        MethodDefinition? moveNextMethod = asyncType.Methods.FirstOrDefault(method => method.Name == "MoveNext");

        if (moveNextMethod != null)
        {
            MethodBodyNode methodBodyNode = new MethodBodyNode(memberName, moveNextMethod, true);

            await AddMethodBodyLinksAsync(methodBodyNode);
        }
    }

    async Task AddLinkToCallMethodAsync(MethodDefinition method, string memberName, MethodReference referencedMethod)
    {
        if (referencedMethod is GenericInstanceMethod genericMethod)
        {
            await genericMethod.GenericArguments.ForEachAsync(genericArg =>
                linkHandler.AddLinkToTypeAsync(memberName, genericArg)
            );
        }

        TypeReference declaringType = referencedMethod.DeclaringType;

        if (IgnoredTypes.IsIgnoredSystemType(declaringType))
            return; // Ignore "System" and "Microsoft" types

        string methodName = Name.GetMethodFullName(referencedMethod);
        if (Name.IsCompilerGenerated(methodName))
            return;

        await linkHandler.AddLinkAsync(memberName, methodName, NodeType.MethodMember);

        TypeReference returnType = referencedMethod.ReturnType;
        if (method.DeclaringType != returnType)
            await linkHandler.AddLinkToTypeAsync(memberName, returnType);

        await referencedMethod
            .Parameters.Select(parameter => parameter.ParameterType)
            .Where(pt => pt != method.DeclaringType)
            .ForEachAsync(parameterType => linkHandler.AddLinkToTypeAsync(memberName, parameterType));
    }
}
