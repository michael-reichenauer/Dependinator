using System;


// ReSharper disable once CheckNamespace
namespace System
{
	internal static class DoubleExtensions
	{
		public static bool Same(this double left, double right) => Math.Abs(left - right) < 0.000001;
	}
}