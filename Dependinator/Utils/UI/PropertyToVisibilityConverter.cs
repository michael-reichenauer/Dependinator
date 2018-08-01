using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.Utils.UI
{
    /// <summary>
    ///     Convert between boolean and visibility
    /// </summary>
    [Localizability(LocalizationCategory.NeverLocalize)]
    public sealed class PropertyToVisibilityConverter : IValueConverter
    {
        /// <summary>
        ///     Convert bool or Nullable&lt;bool&gt; to Visibility
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = false;
            if (value is bool)
            {
                isVisible = (bool)value;
            }
            else if (value is bool?)
            {
                bool? typedValue = (bool?)value;
                isVisible = typedValue ?? false;
            }
            else if (value is Command)
            {
                isVisible = ((Command)value).CanExecute();
            }
            else if (value is int)
            {
                isVisible = (int)value != 0;
            }
            else if (value is string)
            {
                isVisible = !string.IsNullOrEmpty((string)value);
            }
            else if (value is IEnumerable)
            {
                isVisible = IsEmpty((IEnumerable)value);
            }
            else
            {
                isVisible = value != null;
            }

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }


        /// <summary>
        ///     Convert Visibility to boolean
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility)
            {
                return (Visibility)value == Visibility.Visible;
            }

            return false;
        }


        private bool IsEmpty(IEnumerable enumerable)
        {
            foreach (object obj in enumerable)
            {
                return false;
            }

            return true;
        }
    }


    /// <summary>
    ///     Convert between boolean and visibility
    /// </summary>
    [Localizability(LocalizationCategory.NeverLocalize)]
    public sealed class PropertyToVisibilityInvertConverter : IValueConverter
    {
        private readonly PropertyToVisibilityConverter converter = new PropertyToVisibilityConverter();


        /// <summary>
        ///     Convert bool or Nullable&lt;bool&gt; to Visibility
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Visibility visibility = (Visibility)converter.Convert(value, targetType, parameter, culture);
            return visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }


        /// <summary>
        ///     Convert Visibility to boolean
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)converter.ConvertBack(value, targetType, parameter, culture);
        }
    }
}
