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
		private bool isOutgoing = true;
		public Command<Window> CancelCommand => Command<Window>(w => w.Close());


		public ReferencesViewModel(
			IReferenceItemService referenceItemService,
			Node node,
			Line line)
		{
			this.referenceItemService = referenceItemService;
			this.node = node;
			this.line = line;

			SetItems();
	}

		public string SourceNode { get => Get(); set => Set(value); }

		public string TargetNode { get => Get(); set => Set(value); }

		public bool IsShowSwitchButton => line == null;

		public Command SwitchSidesCommand => Command(SwitchSides);

		public ObservableCollection<ReferenceItemViewModel> SourceItems { get; } =
			new ObservableCollection<ReferenceItemViewModel>();

		public ObservableCollection<ReferenceItemViewModel> TargetItems { get; } =
			new ObservableCollection<ReferenceItemViewModel>();



		private async void SetItems()
		{
			if (line == null)
			{
				SetNodeItems();
			}
			else
			{
				SetLineItems();
			}
		}



		private async void SetNodeItems()
		{
			IEnumerable<Line> lines =
				(isOutgoing ? node.SourceLines : node.TargetLines)
				.Where(line => line.Owner != node);

			var nodeItems = await Task.Run(() => referenceItemService.GetReferences(
				lines, new ReferenceOptions(isOutgoing, null, null)));

			if (isOutgoing)
			{
				SourceItems.Clear();
				nodeItems
					.Select(item => new ReferenceItemViewModel(item, this, true))
					.ForEach(item => SourceItems.Add(item));

				SelectNode(node, SourceItems);
			}
			else
			{
				TargetItems.Clear();
				nodeItems
					.Select(item => new ReferenceItemViewModel(item, this, false))
					.ForEach(item => TargetItems.Add(item));

				SelectNode(node, TargetItems);
			}
		}


		private async void SetLineItems()
		{
			var nodeItems = await Task.Run(() => referenceItemService.GetReferences(
				new[] { line }, new ReferenceOptions(isOutgoing, null, null)));

			SourceItems.Clear();
			nodeItems
				.Select(item => new ReferenceItemViewModel(item, this, true))
				.ForEach(item => SourceItems.Add(item));

			SelectNode(line.Source, SourceItems);
		}


		private void SelectNode(Node node1, IEnumerable<ReferenceItemViewModel> items)
		{
			foreach (var viewModel in items)
			{
				if (node1.AncestorsAndSelf().Contains(viewModel.Item.Node))
				{
					viewModel.IsExpanded = true;
					SelectNode(node1, viewModel.SubItems);

					if (viewModel.Item.Node == node1)
					{
						viewModel.IsSelected = true;
						FilterOn(viewModel.Item, isOutgoing);
					}

					break;
				}
			}
		}


		private static void ExpandFirst(IEnumerable<ReferenceItemViewModel> items)
		{
			if (items.Count() == 1)
			{
				var viewModel = items.First();
				viewModel.IsExpanded = true;
				ExpandFirst(viewModel.SubItems);
			}
		}

		

		private void SwitchSides()
		{
			isOutgoing = !isOutgoing;

			SetItems();
		}


		public async void FilterOn(ReferenceItem referenceItem, bool isSource)
		{
			IEnumerable<Line> lines = line == null
				? (isOutgoing ? node.SourceLines : node.TargetLines)
					.Where(line => line.Owner != node)
				: new[] { line };

			if (isSource)
			{
				var referenceItems = await Task.Run(() => referenceItemService.GetReferences(
					lines, new ReferenceOptions(false, referenceItem.Node, null)));

				if (isOutgoing)
				{
					TargetItems.Clear();
					referenceItems
						.Select(item => new ReferenceItemViewModel(item, this, false))
						.ForEach(item => TargetItems.Add(item));
					SourceNode = referenceItem.Node.Name.DisplayFullName;
					TargetNode = line == null ? "*" : line.Target.Name.DisplayFullName;
					ExpandFirst(TargetItems);
				}
				else
				{
					SourceItems.Clear();
					referenceItems
						.Select(item => new ReferenceItemViewModel(item, this, true))
						.ForEach(item => SourceItems.Add(item));
					SourceNode = "*";
					TargetNode = referenceItem.Node.Name.DisplayFullName;
				}
			}
			else
			{
				var nodeItems = await Task.Run(() => referenceItemService.GetReferences(
					lines, new ReferenceOptions(true, node, referenceItem.Node)));

				if (isOutgoing)
				{
					SourceItems.Clear();
					nodeItems
						.Select(item => new ReferenceItemViewModel(item, this, true))
						.ForEach(item => SourceItems.Add(item));
					SourceNode = node.Name.DisplayFullName;
					TargetNode = referenceItem.Node.Name.DisplayFullName;

					ExpandFirst(SourceItems);
				}
				else
				{
					TargetItems.Clear();
					nodeItems
						.Select(item => new ReferenceItemViewModel(item, this, false))
						.ForEach(item => TargetItems.Add(item));
					SourceNode = referenceItem.Node.Name.DisplayFullName;
					TargetNode = node.Name.DisplayFullName;
				}
			}
		}
	}
}