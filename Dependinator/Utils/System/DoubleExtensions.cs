// ReSharper disable once CheckNamespace
namespace System
{
	/// <summary>
	/// 'double' type convenience extensions.
	/// </summary>
	internal static class DoubleExtensions
	{
		public static bool Same(this double left, double right) => Math.Abs(left - right) < 0.000001;


		public static double Rnd(this double i, double round)
		{
			return Math.Round(i / round) * round;
		}


		public static double RoundToNearest(this double amount, double roundTo)
		{
			double excessAmount = amount % roundTo;
			if (excessAmount < (roundTo / 2))
			{
				amount -= excessAmount;
			}
			else
			{
				amount += (roundTo - excessAmount);
			}

			return amount;
		}

	}
}