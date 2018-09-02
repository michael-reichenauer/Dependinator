using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Assemblies.Private
{
    internal class MethodParser
    {
        private readonly Dictionary<string, TypeDefinition> asyncStates =
            new Dictionary<string, TypeDefinition>();


        private readonly LinkHandler linkHandler;
        private readonly List<MethodBodyNode> methodBodyNodes = new List<MethodBodyNode>();


        public MethodParser(LinkHandler linkHandler)
        {
            this.linkHandler = linkHandler;
        }


        public int IlCount { get; private set; } = 0;


        public void AddAsyncStateType(TypeData typeData)
        {
            asyncStates[typeData.Type.FullName] = typeData.Type;
        }


        public void AddMethodLinks(string memberName, MethodDefinition method)
        {
            if (!method.IsConstructor)
            {
                TypeReference returnType = method.ReturnType;
                linkHandler.AddLinkToType(memberName, returnType);
            }


            method.Parameters
                .Select(parameter => parameter.ParameterType)
                .ForEach(parameterType => linkHandler.AddLinkToType(memberName, parameterType));

            methodBodyNodes.Add(new MethodBodyNode(memberName, method, false));
        }

        public void AddMethodBodyLinks(string memberName, MethodDefinition method)
        {
           methodBodyNodes.Add(new MethodBodyNode(memberName, method, false));
        }


        public void AddAllMethodBodyLinks()
        {
            methodBodyNodes.ForEach(AddMethodBodyLinks);
        }


        private void AddMethodBodyLinks(MethodBodyNode methodBodyNode)
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

                body.Variables.ForEach(variable =>
                    AddLinkToMethodVariable(memberName, variable, methodBodyNode.IsMoveNext));

                foreach (Instruction instruction in body.Instructions)
                {
                    IlCount++;
                    if (instruction.Operand is MethodReference methodCall)
                    {
                        AddLinkToCallMethod(memberName, methodCall);
                    }
                    else if (instruction.Operand is FieldDefinition field)
                    {
                        linkHandler.AddLinkToType(memberName, field.FieldType);

                        linkHandler.AddLinkToMember(memberName, field);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception(e);
            }
        }


        private void AddLinkToMethodVariable(
            string memberName, VariableDefinition variable, bool isMoveNext)
        {
            if (!isMoveNext &&
                variable.VariableType.IsNested &&
                asyncStates.TryGetValue(variable.VariableType.FullName, out TypeDefinition asyncType))
            {
                // There is a async state type with this name
                AddAsyncStateLinks(memberName, asyncType);
            }

            linkHandler.AddLinkToType(memberName, variable.VariableType);
        }


        private void AddAsyncStateLinks(string memberName, TypeDefinition asyncType)
        {
            // Try to get the "MovNext method with contains the actual "async/await" code
            MethodDefinition moveNextMethod = asyncType.Methods
                .FirstOrDefault(method => method.Name == "MoveNext");

            if (moveNextMethod != null)
            {
                MethodBodyNode methodBodyNode = new MethodBodyNode(memberName, moveNextMethod, true);

                AddMethodBodyLinks(methodBodyNode);
            }
        }


        private void AddLinkToCallMethod(string memberName, MethodReference method)
        {
            if (method is GenericInstanceMethod genericMethod)
            {
                genericMethod.GenericArguments
                    .ForEach(genericArg => linkHandler.AddLinkToType(memberName, genericArg));
            }

            TypeReference declaringType = method.DeclaringType;

            if (IgnoredTypes.IsIgnoredSystemType(declaringType))
            {
                // Ignore "System" and "Microsoft" namespaces for now
                return;
            }

            string methodName = Name.GetMethodFullName(method);
            if (Name.IsCompilerGenerated(methodName))
            {
                return;
            }

            linkHandler.AddLink(memberName, methodName, NodeData.MemberType);

            TypeReference returnType = method.ReturnType;
            linkHandler.AddLinkToType(memberName, returnType);

            method.Parameters
                .Select(parameter => parameter.ParameterType)
                .ForEach(parameterType => linkHandler.AddLinkToType(memberName, parameterType));
        }


        private class MethodBodyNode
        {
            public MethodBodyNode(string memberName, MethodDefinition method, bool isMoveNext)
            {
                MemberName = memberName;
                Method = method;
                IsMoveNext = isMoveNext;
            }


            public string MemberName { get; }
            public MethodDefinition Method { get; }
            public bool IsMoveNext { get; }
        }
    }
}
