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

		public Command<Window> OkCommand => Command<Window>(SetOK);

		public Command<Window> CancelCommand => Command<Window>(w => w.DialogResult = false);


		public ReferencesViewModel(Node node,
			IEnumerable<ReferenceItem> referenceItems, bool isIncoming)
		{
			Items = new ObservableCollection<ReferenceItemViewModel>(
				referenceItems.Select(item => new ReferenceItemViewModel(item)));

			IsIncoming = isIncoming;
			IsOutgoing = !isIncoming;
			SetTitle(node, isIncoming);
		}


		public string WindowTitle { get => Get(); set => Set(value); }


		public string Title { get => Get(); set => Set(value); }
		public bool IsIncoming { get => Get(); set => Set(value); }
		public bool IsOutgoing{ get => Get(); set => Set(value); }


		public ObservableCollection<ReferenceItemViewModel> Items { get; }


		private void SetOK(Window window) => window.DialogResult = true;


		private void SetTitle(Node node, bool isIncoming)
		{
			if (isIncoming)
			{
				WindowTitle = $"->| {node.Name.DisplayName}";
				Title = $"{node.Name.DisplayFullNoParametersName} incoming references";
			}
			else
			{
				WindowTitle = $"<-| {node.Name.DisplayName}";
				Title = $"{node.Name.DisplayFullNoParametersName} outgoing references";
			}
		}
	}
}