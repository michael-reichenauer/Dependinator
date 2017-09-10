//using System;
//using System.Collections.Generic;
//using System.Reflection;
//using System.Reflection.Emit;
//using Dependinator.Utils;

//namespace Dependinator.ModelParsing.Private.DotNetReflection.Private
//{
//	/// <summary>
//	/// Based on:
//	/// https://www.codeproject.com/Articles/14058/Parsing-the-IL-of-a-Method-Body
//	/// https://blogs.msdn.microsoft.com/haibo_luo/2005/10/04/read-il-from-methodbody/
//	/// </summary>
//	public static class MethodBodyReader
//	{
//		public static string GetIlText(IEnumerable<ILInstruction> instructions) =>
//			IlToText.GetIlText(instructions);


//		/// <summary>
//		/// Parses the array of method body ILInstructions according to the IL byte code.
//		/// </summary>
//		public static IReadOnlyList<ILInstruction> Parse(MethodBase method, MethodBody methodBody)
//		{
//			byte[] bodyIl = methodBody.GetILAsByteArray();
//			Module module = method.Module;
//			Type[] genericMethodArguments;
//			Type[] genericTypeArguments = TryGetGenericTypeArguments(method, out genericMethodArguments);

//			int position = 0;
//			List<ILInstruction> instructions = new List<ILInstruction>();

//			while (position < bodyIl.Length)
//			{
//				int offset = position - 1;
//				object operand = null;
//				OpCode code = ReadOpCode(bodyIl, ref position);

//				int metadataToken = 0;

//				switch (code.OperandType)
//				{
//					case OperandType.InlineBrTarget:
//						metadataToken = ReadInt32(bodyIl, ref position);
//						metadataToken += position;
//						operand = metadataToken;
//						break;

//					case OperandType.InlineField:
//						metadataToken = ReadInt32(bodyIl, ref position);
//						operand = TryResolveIlineField(method, module, metadataToken, genericTypeArguments, genericMethodArguments);
//						break;

//					case OperandType.InlineMethod:
//						metadataToken = ReadInt32(bodyIl, ref position);
//						operand = TryResolveInlineMethod(method, module, metadataToken, genericTypeArguments, genericMethodArguments);
//						break;

//					case OperandType.InlineSig:
//						metadataToken = ReadInt32(bodyIl, ref position);
//						operand = module.ResolveSignature(metadataToken);
//						break;

//					case OperandType.InlineTok:
//						metadataToken = ReadInt32(bodyIl, ref position);
//						operand = TryResolveInlineTok(method, module, metadataToken, genericTypeArguments, genericMethodArguments);
//						break;

//					case OperandType.InlineType:
//						metadataToken = ReadInt32(bodyIl, ref position);
//						operand = TryResolveInlineType(method, module, metadataToken, genericTypeArguments, genericMethodArguments);
//						break;

//					case OperandType.InlineI:
//						operand = ReadInt32(bodyIl, ref position);
//						break;

//					case OperandType.InlineI8:
//						operand = ReadInt64(bodyIl, ref position);
//						break;

//					case OperandType.InlineNone:
//						operand = null;
//						break;

//					case OperandType.InlineR:
//						operand = ReadDouble(bodyIl, ref position);
//						break;

//					case OperandType.InlineString:
//						metadataToken = ReadInt32(bodyIl, ref position);
//						operand = module.ResolveString(metadataToken);
//						break;

//					case OperandType.InlineSwitch:
//						{
//							int count = ReadInt32(bodyIl, ref position);
//							int[] casesAddresses = new int[count];
//							for (int i = 0; i < count; i++)
//							{
//								casesAddresses[i] = ReadInt32(bodyIl, ref position);
//							}
//							int[] cases = new int[count];
//							for (int i = 0; i < count; i++)
//							{
//								cases[i] = position + casesAddresses[i];
//							}
//							break;
//						}

//					case OperandType.InlineVar:
//						operand = ReadUInt16(bodyIl, ref position);
//						break;

//					case OperandType.ShortInlineBrTarget:
//						operand = ReadSByte(bodyIl, ref position) + position;
//						break;

//					case OperandType.ShortInlineI:
//						operand = ReadSByte(bodyIl, ref position);
//						break;

//					case OperandType.ShortInlineR:
//						operand = ReadSingle(bodyIl, ref position);
//						break;

//					case OperandType.ShortInlineVar:
//						operand = ReadByte(bodyIl, ref position);
//						break;

//					default:
//						Log.Warn($"Unknown operand type {code.OperandType} in {method}");
//						throw new Exception("Unknown operand type.");
//				}

//				instructions.Add(new ILInstruction(code, operand, offset));
//			}

//			return instructions;
//		}



//		private static object TryResolveInlineType(
//			MethodBase method,
//			Module module,
//			int metadataToken,
//			Type[] genericTypeArguments,
//			Type[] genericMethodArguments)
//		{
//			if (TryResolveType(
//				module, metadataToken, genericTypeArguments, genericMethodArguments, out object token))
//			{
//				return token;
//			}

//			Log.Error($"Error in {method} for {metadataToken}");
//			return null;
//		}


//		private static object TryResolveInlineTok(
//			MethodBase method,
//			Module module,
//			int metadataToken,
//			Type[] genericTypeArguments,
//			Type[] genericMethodArguments)
//		{
//			if (TryResolveType(
//				module, metadataToken, genericTypeArguments, genericMethodArguments, out object token))
//			{
//				return token;
//			}

//			token = TryResolveInlineMethod(
//				method, module, metadataToken, genericTypeArguments, genericMethodArguments);

//			if (token == null)
//			{
//				Log.Error($"Error in {method.DeclaringType}.{method.Name} for {metadataToken}");
//			}

//			return token;
//		}


//		private static object TryResolveInlineMethod(
//			MethodBase method,
//			Module module,
//			int metadataToken,
//			Type[] genericTypeArguments,
//			Type[] genericMethodArguments)
//		{
//			try
//			{
//				return module.ResolveMethod(metadataToken);
//			}
//			catch
//			{
//				try
//				{
//					return module.ResolveMember(metadataToken);
//				}
//				catch (Exception)
//				{
//					//Try generic method
//					try
//					{
//						return module.ResolveMethod(metadataToken, genericTypeArguments, genericMethodArguments);
//					}
//					catch (Exception)
//					{
//						//Try generic member
//						try
//						{
//							return module.ResolveMember(metadataToken, genericTypeArguments, genericMethodArguments);
//						}
//						catch (Exception e)
//						{
//							Log.Warn($"Error in {method}, {e.Msg()}");
//						}
//					}
//				}
//			}

//			return null;
//		}


//		private static object TryResolveIlineField(
//			MethodBase method,
//			Module module,
//			int metadataToken,
//			Type[] genericTypeArguments,
//			Type[] genericMethodArguments)
//		{
//			try
//			{
//				return module.ResolveField(metadataToken);
//			}
//			catch (Exception)
//			{
//				//Try using generic member
//				try
//				{
//					return module.ResolveField(metadataToken, genericTypeArguments, genericMethodArguments);
//				}
//				catch (Exception e)
//				{
//					Log.Warn($"Error in {method}, {e.Msg()}");
//				}
//			}

//			return null;
//		}


//		private static Type[] TryGetGenericTypeArguments(MethodBase method, out Type[] genericMethodArguments)
//		{
//			Type[] genericTypeArguments = null;
//			genericMethodArguments = null;

//			if (method.DeclaringType?.IsGenericType ?? false)
//			{
//				genericTypeArguments = method.DeclaringType.GetGenericArguments();
//			}

//			if (method.IsGenericMethod)
//			{
//				genericMethodArguments = method.GetGenericArguments();
//			}
//			return genericTypeArguments;
//		}



//		private static bool TryResolveType(
//			Module module,
//			int metadataToken,
//			Type[] genericTypeArguments,
//			Type[] genericMethodArguments,
//			out object type)
//		{
//			if (TryResolveTypeImpl(
//				module, metadataToken, genericTypeArguments, genericMethodArguments, out type))
//			{
//				return true;
//			}

//			////AssemblyName[] assemblyNames = module.Assembly.GetReferencedAssemblies();
//			////foreach (AssemblyName assemblyName in assemblyNames)
//			////{
//			////	if (TryLoadAssembly(assemblyName, out Assembly assembly))
//			////	{
//			////		Module[] modules = assembly.GetModules();
//			////		foreach (Module referencedModule in modules)
//			////		{
//			////			if (TryResolveTypeImpl(
//			////				referencedModule, metadataToken, genericTypeArguments, genericMethodArguments, out type))
//			////			{
//			////				Log.Warn($"Resolved {type}");
//			////				return true;
//			////			}
//			////		}
//			////	}
//			////}

//			return false;
//		}


//		private static bool TryLoadAssembly(AssemblyName assemblyName, out Assembly assembly)
//		{

//			try
//			{
//				assembly = Assembly.ReflectionOnlyLoad(assemblyName.FullName);
//				return true;
//			}
//			catch (Exception)
//			{
//				// Failed to load assembly via name, trying to load via name
//			}

//			try
//			{
//				string name = assemblyName.FullName.Split(',')[0];

//				assembly = Assembly.ReflectionOnlyLoadFrom($"{name}.dll");
//				return true;
//			}
//			catch (Exception e)
//			{
//				Log.Exception(e, $"Could not assembly via name nor file {assemblyName.FullName}");

//				assembly = null;
//				return false;
//			}

//		}


//		private static bool TryResolveTypeImpl(
//			Module module,
//			int metadataToken,
//			Type[] genericTypeArguments,
//			Type[] genericMethodArguments,
//			out object type)
//		{
//			try
//			{
//				type = module.ResolveType(metadataToken);
//				return true;
//			}
//			catch (Exception)
//			{
//				try
//				{
//					if (genericTypeArguments != null || genericMethodArguments != null)
//					{
//						type = module.ResolveType(metadataToken, genericTypeArguments, genericMethodArguments);
//						return true;
//					}
//				}
//				catch (Exception)
//				{
//					// Ignore
//				}
//			}

//			type = null;
//			return false;
//		}


//		private static OpCode ReadOpCode(byte[] bodyIl, ref int position)
//		{
//			OpCode code;
//			ushort value = bodyIl[position++];
//			if (value != 0xfe)
//			{
//				code = Globals.singleByteOpCodes[(int)value];
//			}
//			else
//			{
//				value = bodyIl[position++];
//				code = Globals.multiByteOpCodes[(int)value];
//			}
//			return code;
//		}


//		private static int ReadInt16(byte[] il, ref int position)
//		{
//			return ((il[position++] | (il[position++] << 8)));
//		}

//		private static ushort ReadUInt16(byte[] il, ref int position)
//		{
//			return (ushort)((il[position++] | (il[position++] << 8)));
//		}

//		private static int ReadInt32(byte[] il, ref int position)
//		{
//			return (((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18));
//		}

//		private static ulong ReadInt64(byte[] il, ref int position)
//		{
//			return (ulong)(((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18) | (il[position++] << 0x20) | (il[position++] << 0x28) | (il[position++] << 0x30) | (il[position++] << 0x38));
//		}

//		private static double ReadDouble(byte[] il, ref int position)
//		{
//			return (((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18) | (il[position++] << 0x20) | (il[position++] << 0x28) | (il[position++] << 0x30) | (il[position++] << 0x38));
//		}

//		private static sbyte ReadSByte(byte[] il, ref int position)
//		{
//			return (sbyte)il[position++];
//		}

//		private static byte ReadByte(byte[] il, ref int position)
//		{
//			return (byte)il[position++];
//		}

//		private static Single ReadSingle(byte[] il, ref int position)
//		{
//			return (Single)(((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18));
//		}
//	}
//}