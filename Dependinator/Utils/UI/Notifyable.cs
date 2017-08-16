using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Dependinator.Utils.UI
{
	internal abstract class Notifyable : INotifyPropertyChanged
	{
		private readonly Dictionary<string, Property> properties = new Dictionary<string, Property>();
		private List<TargetWhenSetter> targetWhenSetters;
		private List<SourceWhenSetter> sourceWhenSetters;
		private IReadOnlyList<string> allPropertyNames = null;


		public event PropertyChangedEventHandler PropertyChanged;


		public void Notify(string propertyNames)
		{
			OnPropertyChanged(propertyNames);
		}


		public void Notify(params string[] otherPropertyNames)
		{
			foreach (string otherPropertyName in otherPropertyNames)
			{
				OnPropertyChanged(otherPropertyName);
			}
		}


		public void NotifyAll()
		{
			if (allPropertyNames == null)
			{
				allPropertyNames = GetAllPropertiesNames();
			}

			allPropertyNames.ForEach(OnPropertyChanged);
		}


		public SourceWhenSetter WhenSet(params string[] sourcePropertyName)
		{
			if (sourceWhenSetters == null)
			{
				sourceWhenSetters = new List<SourceWhenSetter>();
			}

			SourceWhenSetter whenSetter = new SourceWhenSetter(this, sourcePropertyName);
			sourceWhenSetters.Add(whenSetter);

			return whenSetter;
		}


		protected TargetWhenSetter WhenSet(Notifyable viewModel, params string[] sourcePropertyName)
		{
			if (targetWhenSetters == null)
			{
				targetWhenSetters = new List<TargetWhenSetter>();
			}

			TargetWhenSetter whenSetter = new TargetWhenSetter(this, viewModel, sourcePropertyName);
			targetWhenSetters.Add(whenSetter);

			return whenSetter;
		}


		protected void OnPropertyChanged(string propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}


		protected Property Get([CallerMemberName] string memberName = "") => GetProperty(memberName);


		protected T Get<T>([CallerMemberName] string memberName = "")
		{
			Property property = Get(memberName);
			if (property.Value == null)
			{
				return default(T);
			}

			return (T)property.Value;
		}


		protected IPropertyNotify Set<T>(T value, [CallerMemberName] string memberName = "")
		{
			Property property = GetProperty(memberName);
			((IPropertySetter)property).Set(value);

			return property;
		}


		private Property GetProperty(string propertyName)
		{
			if (properties.TryGetValue(propertyName, out Property property))
			{
				return property;
			}

			property = new Property(propertyName, this);
			properties[propertyName] = property;

			return property;
		}


		private IReadOnlyList<string> GetAllPropertiesNames()
		{
			return this.GetType()
				.GetProperties()
				.Where(pi => pi.GetGetMethod() != null)
				.Select(pi => pi.Name)
				.ToList();
		}
	}
}