namespace System
{
	public static class ExceptionExtensions
	{
		public static string Msg(this Exception exception)
		{
			if (exception == null)
			{
				return null;
			}

			return $"{exception.GetType()}, {exception.Message}";
		}
	}
}