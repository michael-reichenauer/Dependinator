﻿using System.Globalization;


// ReSharper disable once CheckNamespace
namespace System.Windows
{
    internal static class RectExtensions
    {
        public static string AsString(this Rect rect) => rect.ToString(CultureInfo.InvariantCulture);


        public static string AsIntString(this Rect rect) =>
            new Rect((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height)
                .ToString(CultureInfo.InvariantCulture);


        public static bool Same(this Rect left, Rect right)
            => left.X.Same(right.X)
               && left.Y.Same(right.Y)
               && left.Width.Same(right.Width)
               && left.Height.Same(right.Height);
    }
}
