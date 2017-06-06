using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Dependiator.Utils;


namespace Dependiator.Modeling.Analyzing.Private
{
	/// <summary>
	/// Based on:
	/// https://www.codeproject.com/Articles/14058/Parsing-the-IL-of-a-Method-Body
	/// https://blogs.msdn.microsoft.com/haibo_luo/2005/10/04/read-il-from-methodbody/
	/// </summary>
	public class MethodBodyReader
	{
		/// <summary>
		/// Parses the array of method body ILInstructions according to the IL byte code.
		/// </summary>
		public IReadOnlyList<ILInstruction> Parse(MethodBase method, MethodBody methodBody)
		{
			if (methodBody == null)
			{
				return new List<ILInstruction>();
			}

			byte[] bodyIl = methodBody.GetILAsByteArray();
			Module module = method.Module;
			Type[] genericMethodArguments;
			Type[] genericTypeArguments = TryGetGenericTypeArguments(method, out genericMethodArguments);

			int position = 0;
			List<ILInstruction> instructions = new List<ILInstruction>();
			while (position < bodyIl.Length)
			{
				// get the operation code of the current instruction
				OpCode code = OpCodes.Nop;
				int offset = position - 1;
				object operand = null;

				ushort value = bodyIl[position++];
				if (value != 0xfe)
				{
					code = Globals.singleByteOpCodes[(int)value];
				}
				else
				{
					value = bodyIl[position++];
					code = Globals.multiByteOpCodes[(int)value];
				}

				int metadataToken = 0;

			

				switch (code.OperandType)
				{
					case OperandType.InlineBrTarget:
						metadataToken = ReadInt32(bodyIl, ref position);
						metadataToken += position;
						operand = metadataToken;
						break;
					case OperandType.InlineField:
						try
						{
							metadataToken = ReadInt32(bodyIl, ref position);
							operand = module.ResolveField(metadataToken);
						}
						catch (Exception)
						{
							//Try generic member
							try
							{
								operand = module.ResolveField(metadataToken, genericTypeArguments, genericMethodArguments);
							}
							catch (Exception e)
							{
								Log.Warn($"Error in {method}, {e.Msg()}");
							}
						}

						break;
					case OperandType.InlineMethod:
						metadataToken = ReadInt32(bodyIl, ref position);
						try
						{
							operand = module.ResolveMethod(metadataToken);
						}
						catch
						{
							try
							{
								operand = module.ResolveMember(metadataToken);
							}
							catch (Exception)
							{
								//Try generic method
								try
								{
									operand = module.ResolveMethod(metadataToken, genericTypeArguments, genericMethodArguments);
								}
								catch (Exception)
								{

									//Try generic member
									try
									{
										operand = module.ResolveMember(metadataToken, genericTypeArguments, genericMethodArguments);
									}
									catch (Exception e)
									{
										Log.Warn($"Error in {method}, {e.Msg()}");
									}
								}
							}
						}
						break;

					case OperandType.InlineSig:
						metadataToken = ReadInt32(bodyIl, ref position);
						operand = module.ResolveSignature(metadataToken);
						break;

					case OperandType.InlineTok:
						metadataToken = ReadInt32(bodyIl, ref position);
						try
						{
							operand = module.ResolveType(metadataToken);
						}
						catch
						{
							// Try generic
							try
							{
								operand = module.ResolveType(metadataToken, genericTypeArguments, genericMethodArguments);
							}
							catch (Exception e)
							{
								Log.Warn($"Error in {method}, {e.Msg()}");
							}
						}
						// SSS : see what to do here
						break;

					case OperandType.InlineType:
						metadataToken = ReadInt32(bodyIl, ref position);
						try
						{
							operand = module.ResolveType(metadataToken);
						}
						catch (Exception)
						{
							try
							{
								operand = module.ResolveType(metadataToken, genericTypeArguments, genericMethodArguments);
							}
							catch (Exception e)
							{
								Log.Warn($"Error in {method}, {e.Msg()}");
							}
						
						}
						break;

					case OperandType.InlineI:
						{
							operand = ReadInt32(bodyIl, ref position);
							break;
						}
					case OperandType.InlineI8:
						{
							operand = ReadInt64(bodyIl, ref position);
							break;
						}
					case OperandType.InlineNone:
						{
							operand = null;
							break;
						}
					case OperandType.InlineR:
						{
							operand = ReadDouble(bodyIl, ref position);
							break;
						}
					case OperandType.InlineString:
						{
							metadataToken = ReadInt32(bodyIl, ref position);
							operand = module.ResolveString(metadataToken);
							break;
						}
					case OperandType.InlineSwitch:
						{
							int count = ReadInt32(bodyIl, ref position);
							int[] casesAddresses = new int[count];
							for (int i = 0; i < count; i++)
							{
								casesAddresses[i] = ReadInt32(bodyIl, ref position);
							}
							int[] cases = new int[count];
							for (int i = 0; i < count; i++)
							{
								cases[i] = position + casesAddresses[i];
							}
							break;
						}
					case OperandType.InlineVar:
						{
							operand = ReadUInt16(bodyIl, ref position);
							break;
						}
					case OperandType.ShortInlineBrTarget:
						{
							operand = ReadSByte(bodyIl, ref position) + position;
							break;
						}
					case OperandType.ShortInlineI:
						{
							operand = ReadSByte(bodyIl, ref position);
							break;
						}
					case OperandType.ShortInlineR:
						{
							operand = ReadSingle(bodyIl, ref position);
							break;
						}
					case OperandType.ShortInlineVar:
						{
							operand = ReadByte(bodyIl, ref position);
							break;
						}
					default:
						{
							Log.Warn($"Unknown operand type in {method}");
							throw new Exception("Unknown operand type.");
						}
				}

				instructions.Add(new ILInstruction(code, operand, offset));
			}

			return instructions;
		}


		private static Type[] TryGetGenericTypeArguments(MethodBase method, out Type[] genericMethodArguments)
		{
			Type[] genericTypeArguments = null;
			genericMethodArguments = null;

			if (method.DeclaringType?.IsGenericType ?? false)
			{
				genericTypeArguments = method.DeclaringType.GetGenericArguments();
			}

			if (method.IsGenericMethod)
			{
				genericMethodArguments = method.GetGenericArguments();
			}
			return genericTypeArguments;
		}


		public object GetRefferencedOperand(Module module, int metadataToken)
		{
			AssemblyName[] assemblyNames = module.Assembly.GetReferencedAssemblies();
			for (int i = 0; i < assemblyNames.Length; i++)
			{
				Module[] modules = Assembly.Load(assemblyNames[i]).GetModules();
				for (int j = 0; j < modules.Length; j++)
				{
					try
					{
						Type t = modules[j].ResolveType(metadataToken);
						return t;
					}
					catch (Exception e)
					{
						Log.Warn($"Error: {e.Msg()}");
					}

				}
			}
			return null;
			//System.Reflection.Assembly.Load(module.Assembly.GetReferencedAssemblies()[3]).GetModules()[0].ResolveType(metadataToken)
		}


		private int ReadInt16(byte[] il, ref int position)
		{
			return ((il[position++] | (il[position++] << 8)));
		}
		private ushort ReadUInt16(byte[] il, ref int position)
		{
			return (ushort)((il[position++] | (il[position++] << 8)));
		}
		private int ReadInt32(byte[] il, ref int position)
		{
			return (((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18));
		}
		private ulong ReadInt64(byte[] il, ref int position)
		{
			return (ulong)(((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18) | (il[position++] << 0x20) | (il[position++] << 0x28) | (il[position++] << 0x30) | (il[position++] << 0x38));
		}
		private double ReadDouble(byte[] il, ref int position)
		{
			return (((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18) | (il[position++] << 0x20) | (il[position++] << 0x28) | (il[position++] << 0x30) | (il[position++] << 0x38));
		}
		private sbyte ReadSByte(byte[] il, ref int position)
		{
			return (sbyte)il[position++];
		}
		private byte ReadByte(byte[] il, ref int position)
		{
			return (byte)il[position++];
		}
		private Single ReadSingle(byte[] il, ref int position)
		{
			return (Single)(((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18));
		}
	}
}