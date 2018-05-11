// ReSharper disable once CheckNamespace
namespace System
{
	/// <summary>
	/// 'double' type convieniense extensions.
	/// </summary>
	internal static class DoubleExtensions
	{
		public static bool Same(this double left, double right) => Math.Abs(left - right) < 0.000001;


		public static double Rnd(this double i, double round)
		{
			return Math.Round(i / round) * round;
		}

	}
}