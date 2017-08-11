using System.Reflection.Emit;

namespace Dependinator.Modeling.Private.Analyzing.Private
{
	/// <summary>
	/// Based on:
	/// https://www.codeproject.com/Articles/14058/Parsing-the-IL-of-a-Method-Body
	/// </summary>
	public class ILInstruction
	{
		public OpCode Code { get; }

		public object Operand { get; }

		public int Offset { get; }

		public ILInstruction(OpCode code, object operand, int offset)
		{
			Code = code;
			Operand = operand;
			Offset = offset;
		}

		public override string ToString() => IlToText.AsText(this);
	}
}