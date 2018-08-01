using System.Windows;
using System.Windows.Media;


namespace Dependinator.Utils.UI.Mvvm
{
    internal class Property : IPropertySetter, IPropertyNotify
    {
        private readonly Notifyable notifyable;
        private readonly string propertyName;
        private bool isSet;


        public Property(string propertyName, Notifyable notifyable)
        {
            this.propertyName = propertyName;
            this.notifyable = notifyable;
        }


        internal object Value { get; private set; }

        bool IPropertyNotify.IsSet => isSet;


        void IPropertyNotify.Notify(params string[] otherPropertyNames)
        {
            if (isSet)
            {
                notifyable.Notify(otherPropertyNames);
            }
        }


        void IPropertyNotify.NotifyAll()
        {
            if (isSet)
            {
                notifyable.NotifyAll();
            }
        }


        void IPropertySetter.Set(object value)
        {
            if (!Equals(Value, value))
            {
                Value = value;
                isSet = true;

                notifyable.Notify(propertyName);
            }
            else
            {
                isSet = false;
            }
        }


        public static implicit operator string(Property instance) => (string)instance.Value;
        public static implicit operator bool(Property instance) => (bool?)instance.Value ?? false;
        public static implicit operator int(Property instance) => (int?)instance.Value ?? 0;
        public static implicit operator double(Property instance) => (double?)instance.Value ?? 0;
        public static implicit operator Rect(Property instance) => (Rect?)instance.Value ?? Rect.Empty;
        public static implicit operator Point(Property instance) => (Point?)instance.Value ?? new Point(0, 0);
        public static implicit operator Brush(Property instance) => (Brush)instance.Value;
    }
}
