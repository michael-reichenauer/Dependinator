using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItemViewModel : ViewModel
	{
		public ReferenceItemViewModel(
			IEdge edge, string name, IEnumerable<ReferenceItemViewModel> subItems)
		{
			Items = new ObservableCollection<ReferenceItemViewModel>(subItems);

			Edge = edge;
			Name = name;
		}


		public IEdge Edge { get; }
		public string Name { get=> Get(); set => Set(value); }
		public bool IsSelected { get => Get(); set => Set(value); }
		public bool IsExpanded { get => Get(); set => Set(value); }


		public ObservableCollection<ReferenceItemViewModel> Items { get; }
	}
}