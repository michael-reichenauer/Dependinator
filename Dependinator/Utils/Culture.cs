using System.Globalization;
using System.Threading;


namespace Dependinator.Utils
{
	public static class Culture
	{
		public static void Initialize()
		{
			LogCultures();

			Log.Debug("Setting default culture");
			CultureInfo originalCurrentCulture = CultureInfo.CurrentCulture;
			
			//Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
			//CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");

			Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

			// Set the UI culture to en.US, with some adjustments
			var chosenLanguageCulture = CultureInfo.GetCultureInfo("en-US");

			var numberFormat = (NumberFormatInfo)chosenLanguageCulture.NumberFormat.Clone();
			numberFormat.DigitSubstitution = DigitShapes.None;

			var dateTimeFormatInfo =
				(DateTimeFormatInfo)originalCurrentCulture.DateTimeFormat.Clone();
			//var dateTimeFormatInfo = (DateTimeFormatInfo)culture.DateTimeFormat.Clone();
			dateTimeFormatInfo.Calendar = new GregorianCalendar();

			var culture = (CultureInfo)chosenLanguageCulture.Clone();
			culture.NumberFormat = numberFormat;
			culture.DateTimeFormat = dateTimeFormatInfo;

			Thread.CurrentThread.CurrentUICulture = culture;
			CultureInfo.DefaultThreadCurrentUICulture = culture;

			LogCultures();
		}


		private static void LogCultures()
		{
			Log.Info($"CultureInfo: ");
			Log.Info($"  .DefaultThreadCurrentCulture: {CultureInfo.DefaultThreadCurrentCulture}");
			Log.Info($"  .DefaultThreadCurrentUICulture: {CultureInfo.DefaultThreadCurrentUICulture}");
			Log.Info($"  .InstalledUICulture: {CultureInfo.InstalledUICulture}");
			string shortDatePattern = CultureInfo.InstalledUICulture.DateTimeFormat.ShortDatePattern;
			Log.Info($"  .InstalledUICulture.DateTimeFormat.ShortDatePattern: {shortDatePattern}");

			Log.Info($"Thread.CurrentThread");
			Log.Info($"  .CurrentCulture: {Thread.CurrentThread.CurrentCulture}");
			shortDatePattern = Thread.CurrentThread.CurrentCulture.DateTimeFormat.ShortDatePattern;
			Log.Info($"  .CurrentCulture.DateTimeFormat.ShortDatePattern: {shortDatePattern}");
			Log.Info($"  .CurrentUICulture: {Thread.CurrentThread.CurrentUICulture}");
			string datePattern = Thread.CurrentThread.CurrentUICulture.DateTimeFormat.ShortDatePattern;
			Log.Info($"  .CurrentUICulture.DateTimeFormat.ShortDatePattern: {datePattern}");
		}
	}
}