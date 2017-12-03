namespace Dependinator.Utils
{
	internal static class Config
	{
		public static bool IsDebug
		{
			get
			{
#if DEBUG
				return true;
#else
				return false;
#endif
			}
		}
	}
}