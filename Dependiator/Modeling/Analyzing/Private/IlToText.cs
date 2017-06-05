using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;


namespace Dependiator.Modeling.Analyzing.Private
{
	internal static class IlToText
	{
		/// <summary>
		/// Gets the IL code of the method
		/// </summary>
		/// <returns></returns>
		public static string GetBodyCode(List<ILInstruction> instructions)
		{
			StringBuilder sb = new StringBuilder();

			instructions.ForEach(il => sb.AppendLine(AsText(il)));

			return sb.ToString();
		}

		/// <summary>
		/// Returns a friendly strign representation of this instruction
		/// </summary>
		private static string AsText(ILInstruction il)
		{
			OpCode code = il.Code;
			int offset = il.Offset;
			object operand = il.Operand;

			string result = "";
			result += GetExpandedOffset(offset) + " : " + code;
			if (operand != null)
			{
				switch (code.OperandType)
				{
				case OperandType.InlineField:
					System.Reflection.FieldInfo fOperand = ((System.Reflection.FieldInfo)operand);
					result += " " + Globals.ProcessSpecialTypes(fOperand.FieldType.ToString()) + " " +
					          Globals.ProcessSpecialTypes(fOperand.ReflectedType.ToString()) +
					          "::" + fOperand.Name + "";
					break;
				case OperandType.InlineMethod:
					try
					{
						System.Reflection.MethodInfo mOperand = (System.Reflection.MethodInfo)operand;
						result += " ";
						if (!mOperand.IsStatic) result += "instance ";
						result += Globals.ProcessSpecialTypes(mOperand.ReturnType.ToString()) +
						          " " + Globals.ProcessSpecialTypes(mOperand.ReflectedType.ToString()) +
						          "::" + mOperand.Name + "()";
					}
					catch
					{
						try
						{
							System.Reflection.ConstructorInfo mOperand = (System.Reflection.ConstructorInfo)operand;
							result += " ";
							if (!mOperand.IsStatic) result += "instance ";
							result += "void " +
							          Globals.ProcessSpecialTypes(mOperand.ReflectedType.ToString()) +
							          "::" + mOperand.Name + "()";
						}
						catch
						{
						}
					}
					break;
				case OperandType.ShortInlineBrTarget:
				case OperandType.InlineBrTarget:
					result += " " + GetExpandedOffset((int)operand);
					break;
				case OperandType.InlineType:
					result += " " + Globals.ProcessSpecialTypes(operand.ToString());
					break;
				case OperandType.InlineString:
					if (operand.ToString() == "\r\n") result += " \"\\r\\n\"";
					else result += " \"" + operand.ToString() + "\"";
					break;
				case OperandType.ShortInlineVar:
					result += operand.ToString();
					break;
				case OperandType.InlineI:
				case OperandType.InlineI8:
				case OperandType.InlineR:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineR:
					result += operand.ToString();
					break;
				case OperandType.InlineTok:
					if (operand is Type)
						result += ((Type)operand).FullName;
					else
						result += "not supported";
					break;

				default: result += "not supported"; break;
				}
			}
			return result;

		}

		/// <summary>
		/// Add enough zeros to a number as to be represented on 4 characters
		/// </summary>
		private static string GetExpandedOffset(long offset)
		{
			string result = offset.ToString();
			for (int i = 0; result.Length < 4; i++)
			{
				result = "0" + result;
			}
			return result;
		}
	}
}