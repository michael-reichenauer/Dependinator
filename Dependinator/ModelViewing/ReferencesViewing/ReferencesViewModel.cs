using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferencesViewModel : ViewModel
	{
		private readonly IReferenceItemService referenceItemService;
		public Command<Window> CancelCommand => Command<Window>(w => w.Close());


		public ReferencesViewModel(
			IReferenceItemService referenceItemService,
			Node node,
			Line line,
			bool isIncoming)
		{
			this.referenceItemService = referenceItemService;

			IsIncoming = isIncoming;
			IsOutgoing = !isIncoming;
			SetSourceTarget(node, isIncoming);

			SetItems(node, line, isIncoming);
		}




		public string SourceNode { get => Get(); set => Set(value); }

		public string TargetNode { get => Get(); set => Set(value); }


		public bool IsIncoming { get => Get(); set => Set(value); }


		public bool IsOutgoing { get => Get(); set => Set(value); }


		public ObservableCollection<ReferenceItemViewModel> SourceItems { get; } =
			new ObservableCollection<ReferenceItemViewModel>();


		public ObservableCollection<ReferenceItemViewModel> TargetItems { get; } =
			new ObservableCollection<ReferenceItemViewModel>();


		private async void SetItems(Node node, Line line, bool isIncoming)
		{
			IEnumerable<ReferenceItem> nodeItems =
				await Task.Run(() => referenceItemService.GetReferences(
					node, new ReferenceOptions(true, isIncoming)));

			IEnumerable<ReferenceItem> referenceItems = line == null
				? await Task.Run(() => referenceItemService.GetReferences(
					node, new ReferenceOptions(false, isIncoming)))
				: await Task.Run(() => referenceItemService.GetReferences(
					line, new ReferenceOptions(false, isIncoming)));


			if (!isIncoming)
			{
				nodeItems
					.Select(item => new ReferenceItemViewModel(item))
					.ForEach(item => SourceItems.Add(item));

				referenceItems
					.Select(item => new ReferenceItemViewModel(item))
					.ForEach(item => TargetItems.Add(item));
			}

			else
			{
				referenceItems
					.Select(item => new ReferenceItemViewModel(item))
					.ForEach(item => SourceItems.Add(item));

				nodeItems
					.Select(item => new ReferenceItemViewModel(item))
					.ForEach(item => TargetItems.Add(item));
			}
		}


		private void SetSourceTarget(Node node, bool isIncoming)
		{
			if (isIncoming)
			{
				SourceNode = "*";
				TargetNode = node.Name.DisplayFullName;
			}
			else
			{
				SourceNode = node.Name.DisplayFullName;
				TargetNode = "*";
			}
		}
	}
}