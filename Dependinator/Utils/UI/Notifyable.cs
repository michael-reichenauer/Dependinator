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
		private IList<string> allPropertyNames = null;


		public event PropertyChangedEventHandler PropertyChanged;


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
				GetAllPropertiesNames();
			}

			foreach (string propertyName in allPropertyNames)
			{
				OnPropertyChanged(propertyName);
			}
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


		protected Property Get([CallerMemberName] string memberName = "")
		{
			if (properties.TryGetValue(memberName, out Property property))
			{
				return property;
			}

			property = new Property(memberName, OnPropertyChanged, this);
			properties[memberName] = property;

			return property;
		}


		protected T Get<T>([CallerMemberName] string memberName = "")
		{
			Property property = Get(memberName);
			if (property.Value == null)
			{
				return default(T);
			}

			return (T)property.Value;
		}


		protected PropertySetter Set<T>(T value, [CallerMemberName] string memberName = "")
		{
			Property property = Get(memberName);
			return property.Set(value);
		}


		private void GetAllPropertiesNames()
		{
			allPropertyNames = this.GetType()
				.GetProperties()
				.Where(pi => pi.GetGetMethod() != null)
				.Select(pi => pi.Name)
				.ToList();
		}
	}
}