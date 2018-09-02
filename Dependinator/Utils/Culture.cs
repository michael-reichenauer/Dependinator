using System.Globalization;
using System.Threading;


namespace Dependinator.Utils
{
    public static class Culture
    {
        /// <summary>
        /// Sets the default culture to Invariant, with some minor adjustments to date/time format.
        /// This makes string usage throughout the program more predictable
        /// </summary>
        public static void SetDefaultInvariantCulture()
        {
            CultureInfo originalCurrentCulture = CultureInfo.CurrentCulture;

            CultureInfo threadCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            DateTimeFormatInfo dateTimeFormat =
                (DateTimeFormatInfo)CultureInfo.GetCultureInfo("sv-SE").DateTimeFormat.Clone();
            threadCulture.DateTimeFormat = dateTimeFormat;

            // Make sure current (main) and new worker threads have the same invariant culture 
            Thread.CurrentThread.CurrentCulture = threadCulture;
            CultureInfo.DefaultThreadCurrentCulture = threadCulture;

            // Set the UI culture to en.US, with some adjustments
            var chosenLanguageCulture = CultureInfo.GetCultureInfo("en-US");

            var numberFormat = (NumberFormatInfo)chosenLanguageCulture.NumberFormat.Clone();
            numberFormat.DigitSubstitution = DigitShapes.None;

            var dateTimeFormatInfo =
                (DateTimeFormatInfo)originalCurrentCulture.DateTimeFormat.Clone();

            dateTimeFormatInfo.Calendar = new GregorianCalendar();

            var culture = (CultureInfo)chosenLanguageCulture.Clone();
            culture.NumberFormat = numberFormat;
            culture.DateTimeFormat = dateTimeFormatInfo;

            // Make sure current (main) and new worker threads have the same UI culture 
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}
