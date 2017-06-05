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
		public List<ILInstruction> instructions = null;
		protected byte[] il = null;
		private MethodBase mi = null;


		/// <summary>
		/// Constructs the array of ILInstructions according to the IL byte code.
		/// </summary>
		/// <param name="module"></param>
		private void ConstructInstructions(Module module)
		{
			if (mi.IsGenericMethod)
			{

			}

			byte[] il = this.il;
			int position = 0;
			instructions = new List<ILInstruction>();
			while (position < il.Length)
			{
				//ILInstruction instruction = new ILInstruction();

				// get the operation code of the current instruction
				OpCode code = OpCodes.Nop;
				int offset = position - 1;
				object operand = null;

				ushort value = il[position++];
				if (value != 0xfe)
				{
					code = Globals.singleByteOpCodes[(int)value];
				}
				else
				{
					value = il[position++];
					code = Globals.multiByteOpCodes[(int)value];
				}

				int metadataToken = 0;

				// get the operand of the current operation
				switch (code.OperandType)
				{
					case OperandType.InlineBrTarget:
						metadataToken = ReadInt32(il, ref position);
						metadataToken += position;
						operand = metadataToken;
						break;
					case OperandType.InlineField:
						try
						{
							metadataToken = ReadInt32(il, ref position);
							operand = module.ResolveField(metadataToken);
						}
						catch (Exception)
						{
							//Try generic member
							try
							{
								operand = module.ResolveField(metadataToken, mi.DeclaringType.GetGenericArguments(), mi.GetGenericArguments());
							}
							catch (Exception e)
							{
								Log.Warn($"Error in {mi}, {e.Message}");
							}
						}

						break;
					case OperandType.InlineMethod:
						metadataToken = ReadInt32(il, ref position);
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
									operand = module.ResolveMethod(metadataToken, mi.DeclaringType.GetGenericArguments(), mi.GetGenericArguments());
								}
								catch (Exception)
								{

									//Try generic member
									try
									{
										operand = module.ResolveMember(metadataToken, mi.DeclaringType.GetGenericArguments(), mi.GetGenericArguments());
									}
									catch (Exception e)
									{
										Log.Warn($"Error in {mi}, {e.Message}");
									}
								}

							}
						}
						break;
					case OperandType.InlineSig:
						metadataToken = ReadInt32(il, ref position);
						operand = module.ResolveSignature(metadataToken);
						break;
					case OperandType.InlineTok:
						metadataToken = ReadInt32(il, ref position);
						try
						{
							operand = module.ResolveType(metadataToken);
						}
						catch (Exception)
						{
							// Try generic
							try
							{
								operand = module.ResolveType(metadataToken, this.mi.DeclaringType.GetGenericArguments(), this.mi.GetGenericArguments());
							}
							catch (Exception e)
							{
								Log.Warn($"Error in {mi}, {e.Message}");
							}
						}
						// SSS : see what to do here
						break;
					case OperandType.InlineType:
						metadataToken = ReadInt32(il, ref position);
						// now we call the ResolveType always using the generic attributes type in order
						// to support decompilation of generic methods and classes

						// thanks to the guys from code project who commented on this missing feature
						try
						{
							operand = module.ResolveType(metadataToken);
						}
						catch (Exception)
						{
							operand = module.ResolveType(metadataToken, this.mi.DeclaringType.GetGenericArguments(), this.mi.GetGenericArguments());
						}
						break;
					case OperandType.InlineI:
						{
							operand = ReadInt32(il, ref position);
							break;
						}
					case OperandType.InlineI8:
						{
							operand = ReadInt64(il, ref position);
							break;
						}
					case OperandType.InlineNone:
						{
							operand = null;
							break;
						}
					case OperandType.InlineR:
						{
							operand = ReadDouble(il, ref position);
							break;
						}
					case OperandType.InlineString:
						{
							metadataToken = ReadInt32(il, ref position);
							operand = module.ResolveString(metadataToken);
							break;
						}
					case OperandType.InlineSwitch:
						{
							int count = ReadInt32(il, ref position);
							int[] casesAddresses = new int[count];
							for (int i = 0; i < count; i++)
							{
								casesAddresses[i] = ReadInt32(il, ref position);
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
							operand = ReadUInt16(il, ref position);
							break;
						}
					case OperandType.ShortInlineBrTarget:
						{
							operand = ReadSByte(il, ref position) + position;
							break;
						}
					case OperandType.ShortInlineI:
						{
							operand = ReadSByte(il, ref position);
							break;
						}
					case OperandType.ShortInlineR:
						{
							operand = ReadSingle(il, ref position);
							break;
						}
					case OperandType.ShortInlineVar:
						{
							operand = ReadByte(il, ref position);
							break;
						}
					default:
						{
							Log.Warn($"Unknown operand type in {mi}");
							throw new Exception("Unknown operand type.");
						}
				}

				instructions.Add(new ILInstruction(code, operand, offset));
			}
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
					catch
					{

					}

				}
			}
			return null;
			//System.Reflection.Assembly.Load(module.Assembly.GetReferencedAssemblies()[3]).GetModules()[0].ResolveType(metadataToken)
		}


		private int ReadInt16(byte[] _il, ref int position)
		{
			return ((il[position++] | (il[position++] << 8)));
		}
		private ushort ReadUInt16(byte[] _il, ref int position)
		{
			return (ushort)((il[position++] | (il[position++] << 8)));
		}
		private int ReadInt32(byte[] _il, ref int position)
		{
			return (((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18));
		}
		private ulong ReadInt64(byte[] _il, ref int position)
		{
			return (ulong)(((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18) | (il[position++] << 0x20) | (il[position++] << 0x28) | (il[position++] << 0x30) | (il[position++] << 0x38));
		}
		private double ReadDouble(byte[] _il, ref int position)
		{
			return (((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18) | (il[position++] << 0x20) | (il[position++] << 0x28) | (il[position++] << 0x30) | (il[position++] << 0x38));
		}
		private sbyte ReadSByte(byte[] _il, ref int position)
		{
			return (sbyte)il[position++];
		}
		private byte ReadByte(byte[] _il, ref int position)
		{
			return (byte)il[position++];
		}
		private Single ReadSingle(byte[] _il, ref int position)
		{
			return (Single)(((il[position++] | (il[position++] << 8)) | (il[position++] << 0x10)) | (il[position++] << 0x18));
		}




		/// <summary>
		/// MethodBodyReader constructor
		/// </summary>
		/// <param name="mi">
		///   The System.Reflection defined MethodInfo
		/// </param>
		/// <param name="methodBody"></param>
		public MethodBodyReader(MethodBase mi, MethodBody methodBody)
		{
			try
			{
				this.mi = mi;
				if (methodBody != null)
				{
					il = methodBody.GetILAsByteArray();
					ConstructInstructions(mi.Module);
				}
			}
			catch (Exception e)
			{
				Log.Warn($"Error {e}");
				throw;
			}
		}
	}
}