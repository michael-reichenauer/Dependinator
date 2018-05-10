using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferencesViewModel : ViewModel
	{
		private readonly IReferenceItemService referenceItemService;


		public Command<Window> OkCommand => Command<Window>(SetOK);

		public Command<Window> CancelCommand => Command<Window>(w => w.DialogResult = false);


		public ReferencesViewModel(
			IReferenceItemService referenceItemService,
			Node node)
		{
			this.referenceItemService = referenceItemService;

			//IEnumerable<Line> lines = node.TargetLines
			//	.Where(line => line.Owner != node);

			//IEnumerable<ReferenceItemViewModel> referenceItems = referenceItemService.GetSourceLinkItems(
			//	lines);

		

			var referenceItems = referenceItemService.GetOutgoingReferences(node);


			Items = new ObservableCollection<ReferenceItemViewModel>(referenceItems);
			//referenceItemViewModel.IsSelected = true;

			WindowTitle = $"<- {node.Name.DisplayName}";
			Title = $"Incoming: {node.Name.DisplayFullName}";
		}


		public string WindowTitle { get => Get(); set => Set(value); }


		public string Title { get => Get(); set => Set(value); }


		public ObservableCollection<ReferenceItemViewModel> Items { get; }



		private void SetOK(Window window) => window.DialogResult = true;
	}
}