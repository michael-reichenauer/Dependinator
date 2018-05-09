using System.Collections.Generic;
using System.Collections.ObjectModel;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItemViewModel : ViewModel
	{
		public ReferenceItemViewModel(
			IEdge edge, string text, IEnumerable<ReferenceItemViewModel> subItems)
		{
			SubItems = new ObservableCollection<ReferenceItemViewModel>(subItems);

			Edge = edge;
			Text = text;
		}


		public IEdge Edge { get; }
		public string Text { get=> Get(); set => Set(value); }
		public bool IsSelected { get => Get(); set => Set(value); }
		public bool IsExpanded { get => Get(); set => Set(value); }


		public ObservableCollection<ReferenceItemViewModel> SubItems { get; }
	}
}