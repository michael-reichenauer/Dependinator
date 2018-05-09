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

			IEnumerable<Line> lines = node.SourceLines
				.Where(line => line.Owner != node);

			IEnumerable<ReferenceItemViewModel> referenceItems = referenceItemService.GetTargetLinkItems(
				lines);


			Items = new ObservableCollection<ReferenceItemViewModel>(referenceItems);
			//referenceItemViewModel.IsSelected = true;

			WindowTitle = $"-> {node.Name.DisplayName}";
			Title = $"Incoming: {node.Name.DisplayFullName}";
		}


		public string WindowTitle { get => Get(); set => Set(value); }


		public string Title { get => Get(); set => Set(value); }


		public ObservableCollection<ReferenceItemViewModel> Items { get; }


		private void InitItems(Node node)
		{
			throw new System.NotImplementedException();
		}


		//private ObservableCollection<ReferenceItemViewModel> InitItems(
		//	Node node, out ReferenceItemViewModel referenceItemViewModel)
		//{
		//	referenceItemViewModel = null;
		//	ObservableCollection<ReferenceItemViewModel> items = ReferenceItems;

		//	if (!node.IsRoot)
		//	{
		//		items = InitItems(node.Parent, out ReferenceItemViewModel parent);

		//		referenceItemViewModel = new ReferenceItemViewModel(node.Name.DisplayName);
		//		items.Add(referenceItemViewModel);
		//		//referenceItemViewModel.IsExpanded = true;

		//		items = referenceItemViewModel.Items;
		//	}

		//	return items;
		//}


		private void SetOK(Window window) => window.DialogResult = true;
	}
}