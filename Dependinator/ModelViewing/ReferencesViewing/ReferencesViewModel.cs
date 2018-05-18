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
		private readonly Node node;
		private readonly Line line;
		private bool isIncoming;
		public Command<Window> CancelCommand => Command<Window>(w => w.Close());


		public ReferencesViewModel(
			IReferenceItemService referenceItemService,
			Node node,
			Line line)
		{
			this.referenceItemService = referenceItemService;
			this.node = node;
			this.line = line;
			this.isIncoming = false;

			SetSourceAndTarget();
		}

		public string SourceNode { get => Get(); set => Set(value); }

		public string TargetNode { get => Get(); set => Set(value); }

		public bool IsShowSwitchButton => line == null;

		public Command SwitchSidesCommand => Command(SwitchSides);

		public ObservableCollection<ReferenceItemViewModel> SourceItems { get; } =
			new ObservableCollection<ReferenceItemViewModel>();

		public ObservableCollection<ReferenceItemViewModel> TargetItems { get; } =
			new ObservableCollection<ReferenceItemViewModel>();



		private void SetSourceAndTarget()
		{
			SetSourceTarget();
			SetItems();
		}

		private async void SetItems()
		{
			SourceItems.Clear();
			TargetItems.Clear();

			IEnumerable<ReferenceItem> nodeItems;
			IEnumerable<ReferenceItem> referenceItems;

			if (line == null)
			{
				nodeItems = await Task.Run(() => referenceItemService.GetReferences(
						node, new ReferenceOptions(true, isIncoming)));
				referenceItems = await Task.Run(() => referenceItemService.GetReferences(
					node, new ReferenceOptions(false, isIncoming)));

			}
			else
			{
				nodeItems = await Task.Run(() => referenceItemService.GetReferences(
					line, new ReferenceOptions(true, isIncoming)));

				referenceItems = await Task.Run(() => referenceItemService.GetReferences(
					line, new ReferenceOptions(false, isIncoming)));
			}
			

			if (!isIncoming)
			{
				nodeItems
					.Select(item => new ReferenceItemViewModel(item))
					.ForEach(item => SourceItems.Add(item));

				SourceItems.First().IsExpanded = true;
				SourceItems.First().IsSelected = true;

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

				if (TargetItems.Any())
				{
					TargetItems.First().IsExpanded = true;
					TargetItems.First().IsSelected = true;
				}
			}
		}


		private void SetSourceTarget()
		{
			if (!isIncoming)
			{
				SourceNode = line == null ? node.Name.DisplayFullName : line.Source.Name.DisplayFullName;
				TargetNode = line == null ? "*" : line.Target.Name.DisplayFullName;
			}
			else
			{
				SourceNode = line == null ? "*" : line.Target.Name.DisplayFullName;
				TargetNode = line == null ? node.Name.DisplayFullName : line.Source.Name.DisplayFullName;
			}
		}



		private void SwitchSides()
		{
			isIncoming = !isIncoming;

			SetSourceAndTarget();
		}
	}
}