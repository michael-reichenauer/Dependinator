﻿using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using ProtoBuf;
using ProtoBuf.Meta;
using ProtoSerializer = ProtoBuf.Serializer;


namespace Dependinator.Utils.Serialization
{
    public static class ProtoBufSerializer
    {
        public static void Serialize(FileStream file, object data)
        {
            ProtoSerializer.Serialize(file, data);
        }


        public static T Deserialize<T>(FileStream file)
        {
            return ProtoSerializer.Deserialize<T>(file);
        }


        public static T DeserializeWithLengthPrefix<T>(FileStream file)
        {
            return ProtoSerializer.DeserializeWithLengthPrefix<T>(file, PrefixStyle.Fixed32);
        }


        public static void RegisterDataContractTypes(Assembly assembly)
        {
            // Get all types with [DataContract] attribute
            var dataContractTypes = assembly
                .GetTypes()
                .Where(HasDataContractAttribute);

            // Register data contract types
            dataContractTypes.ForEach(RegisterType);
        }


        private static void RegisterType(Type dataContractType)
        {
            HandleSurrogateType(dataContractType);

            // Disable default handling of that type
            MetaType metaType = RuntimeTypeModel.Default.Add(dataContractType, false);

            // Get members with [DataMember] attribute
            var names = dataContractType
                .GetMembers()
                .Where(p => p.GetCustomAttribute<DataMemberAttribute>() != null)
                .Select(property => property.Name);

            // Add these member names to serialization,
            names.ForEach(name => metaType.Add(name));
        }


        private static void HandleSurrogateType(Type dataContractType)
        {
            var typeConverterAttribute = TryGetTypeConverterAttribute(dataContractType);

            if (typeConverterAttribute != null)
            {
                // Type is a surrogate type for some other replaced type
                Type replacedType = Type.GetType(typeConverterAttribute.ConverterTypeName);
                Type surrogateType = dataContractType;

                RuntimeTypeModel.Default.Add(replacedType, false).SetSurrogate(surrogateType);
            }
        }


        private static TypeConverterAttribute TryGetTypeConverterAttribute(Type dataContractType)
        {
            return dataContractType.GetCustomAttribute<TypeConverterAttribute>();
        }


        private static bool HasDataContractAttribute(Type type)
        {
            return type.GetCustomAttributes(typeof(DataContractAttribute), false).Any();
        }
    }
}
