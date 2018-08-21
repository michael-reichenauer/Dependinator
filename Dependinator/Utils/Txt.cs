using System.Collections.Generic;
using System.Globalization;


namespace System
{
    internal static class Txt
    {
        public static int Compare(string strA, string strB) =>
            string.Compare(strA, strB, StringComparison.Ordinal);


        public static int CompareIgnoreCase(string strA, string strB) =>
            string.Compare(strA, strB, StringComparison.OrdinalIgnoreCase);


        public static bool IsSame(this string strA, string strB) =>
            0 == string.Compare(strA, strB, StringComparison.Ordinal);


        public static bool IsSameIc(this string strA, string strB) =>
            0 == string.Compare(strA, strB, StringComparison.OrdinalIgnoreCase);


        public static int IndexOfTxt(this string text, string value) =>
            text.IndexOf(value, StringComparison.Ordinal);


        public static int IndexOfTxtIc(this string text, string value) =>
            text.IndexOf(value, StringComparison.OrdinalIgnoreCase);


        public static int IndexOfTxt(this string text, string value, int index) =>
            text.IndexOf(value, index, StringComparison.Ordinal);


        public static int IndexOfTxtIc(this string text, string value, int index) =>
            text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase);


        public static bool StartsWithTxt(this string text, string value) =>
            text.StartsWith(value, StringComparison.Ordinal);


        public static bool StartsWithIc(this string text, string value) =>
            text.StartsWith(value, StringComparison.OrdinalIgnoreCase);


        public static string I(FormattableString formattable)
        {
            return formattable.ToString(CultureInfo.InvariantCulture);
        }


        public static IEnumerable<string> Parts(this string text, char splitChar)
        {
            int index1 = 0;
            int index2 = text.IndexOf(splitChar);

            while (index2 > -1)
            {
                string part = text.Substring(index1, index2 - index1);
                index1 = index2 + 1;
                index2 = text.IndexOf(splitChar, index2 + 1);

                yield return part;
            }
        }
    }
}
