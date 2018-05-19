using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;
using Mono.CSharp.Linq;


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

			var nodeItems = await Task.Run(() => referenceItemService.GetReferences2(
				new ReferenceOptions2(lines, true, node, null)));

			if (isOutgoing)
			{
				SourceItems.Clear();
				nodeItems
					.Select(item => new ReferenceItemViewModel(item, this, true))
					.ForEach(item => SourceItems.Add(item));

				SelectNode(node, SourceItems);

				//if (SourceItems.Any())
				//{
				//	//SourceItems.First().IsExpanded = true;

				//	//FilterOn(nodeItems.First(), isOutgoing);
				//	SelectNode(node, SourceItems);
				//	//SourceItems.First().IsSelected = true;
				//}
			}

			else
			{
				TargetItems.Clear();
				nodeItems
					.Select(item => new ReferenceItemViewModel(item, this, false))
					.ForEach(item => TargetItems.Add(item));

				if (TargetItems.Any())
				{
					TargetItems.First().IsExpanded = true;
					FilterOn(nodeItems.First(), isOutgoing);
					//TargetItems.First().IsSelected = true;
				}
			}
		}


		private void SelectNode(Node node1, IEnumerable<ReferenceItemViewModel> items)
		{
			foreach (var viewModel in items)
			{
				if (node1.AncestorsAndSelf().Contains(viewModel.Item.Node))
				{
					viewModel.IsExpanded = true;
					SelectNode(node, viewModel.SubItems);

					if (viewModel.Item.Node == node)
					{
						viewModel.IsSelected = true;
						FilterOn(viewModel.Item, isOutgoing);
					}

					break;
				}
			}
		}


		private void SelectFirst(IEnumerable<ReferenceItemViewModel> items)
		{
			if (items.Count() > 1)
			{
				return;
			}

			foreach (var viewModel in items)
			{
				if (viewModel.SubItems.Count < 2)
				{
					viewModel.IsExpanded = true;
					SelectFirst(viewModel.SubItems);
					break;
				}
			}
		}


		private async void SetLineItems()
		{
			var nodeItems = await Task.Run(() => referenceItemService.GetReferences(
				line, new ReferenceOptions(true, !isOutgoing)));

			SourceItems.Clear();
			nodeItems
				.Select(item => new ReferenceItemViewModel(item, this, true))
				.ForEach(item => SourceItems.Add(item));

			if (SourceItems.Any())
			{
				SourceItems.First().IsExpanded = true;
				FilterOn(nodeItems.First(), isOutgoing);
				//SourceItems.First().IsSelected = true;
			}
		}



		private void SetSourceTarget()
		{
			if (isOutgoing)
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
			isOutgoing = !isOutgoing;

			SetSourceAndTarget();
		}


		public async void FilterOn(ReferenceItem referenceItem, bool isSource)
		{
			IEnumerable<Line> lines =
				(isOutgoing ? node.SourceLines : node.TargetLines)
				.Where(line => line.Owner != node);

			if (isSource)
			{
				var referenceItems = await Task.Run(() => referenceItemService.GetReferences2(
					new ReferenceOptions2(lines, false, referenceItem.Node, null)));

				if (isOutgoing)
				{
					TargetItems.Clear();
					referenceItems
						.Select(item => new ReferenceItemViewModel(item, this, false))
						.ForEach(item => TargetItems.Add(item));
					SourceNode = referenceItem.Node.Name.DisplayFullName;
					TargetNode = "*";
					SelectFirst(TargetItems);
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
				var nodeItems = await Task.Run(() => referenceItemService.GetReferences2(
					new ReferenceOptions2(lines, true, node, referenceItem.Node)));

				if (isOutgoing)
				{
					SourceItems.Clear();
					nodeItems
						.Select(item => new ReferenceItemViewModel(item, this, true))
						.ForEach(item => SourceItems.Add(item));
					SourceNode = node.Name.DisplayFullName;
					TargetNode = referenceItem.Node.Name.DisplayFullName;

					//if (SourceItems.Any())
					//{
					//	SourceItems.First().IsExpanded = true;
					//	//SourceItems.First().SetIsSelected();
					//}

					SelectFirst(SourceItems);
				}
				else
				{
					TargetItems.Clear();
					nodeItems
						.Select(item => new ReferenceItemViewModel(item, this, false))
						.ForEach(item => TargetItems.Add(item));
					SourceNode = referenceItem.Node.Name.DisplayFullName;
					TargetNode = node.Name.DisplayFullName;
					//if (TargetItems.Any())
					//{
					//	TargetItems.First().IsExpanded = true;
					//	//TargetItems.First().SetIsSelected();
					//}
				}
			}
		}
	}
}