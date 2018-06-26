using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependenciesService : IDependenciesService
	{
		public async Task<IReadOnlyList<DependencyItem>> GetDependencyItemsAsync(
			bool isSource,
			Node sourceNode,
			Node targetNode)
		{
			return await Task.Run(() =>
			{
				Options options = new Options(isSource, sourceNode, targetNode);

				IEnumerable<Link> links = GetLinks(options);

				var items = CreateReferenceHierarchy(links, options);

				DependencyItem rootItem = items[NodeName.Root];

				AddMainNodeIfNeeded(rootItem, options);

				return new List<DependencyItem> { rootItem };
			});
		}


		private static IEnumerable<Link> GetLinks(Options options)
		{
			if (options.IsSource)
			{
				return options.SourceNode
					.DescendentsAndSelf()
					.SelectMany(node => node.SourceLinks)
					.Where(link => IsIncluded(link, options));
			}
			else
			{
				return options.TargetNode
					.DescendentsAndSelf()
					.SelectMany(node => node.TargetLinks)
					.Where(link => IsIncluded(link, options));
			}
		}


		private void AddMainNodeIfNeeded(DependencyItem rootItem, Options options)
		{
			Node mainNode = options.IsSource ? options.SourceNode : options.TargetNode;

			if (rootItem.SubItems.Any() || mainNode.IsRoot)
			{
				return;
			}

			DependencyItem current = rootItem;

			foreach (Node node in mainNode.AncestorsAndSelf().Reverse())
			{
				DependencyItem subItem = new DependencyItem(node.Name, node.HasCode);
				current.AddChild(subItem);
				current = subItem;
			}
		}


		private static bool IsIncluded(IEdge link, Options options)
		{
			if (link.Source.View.IsHidden || link.Target.View.IsHidden)
			{
				// Always exclude hidden nodes links
				return false;
			}


			return
				(link.Source.AncestorsAndSelf().Contains(options.SourceNode) ) &&
				(link.Target.AncestorsAndSelf().Contains(options.TargetNode) &&
				 !link.Target.AncestorsAndSelf().Contains(options.SourceNode));



			//return !link.Target.View.IsHidden &&
			//       (options.SourceNode.IsRoot || link.Source.AncestorsAndSelf().Contains(options.SourceNode)) &&
			//       (options.TargetNode.IsRoot || link.Target.AncestorsAndSelf().Contains(options.TargetNode));
		}


		private Dictionary<NodeName, DependencyItem> CreateReferenceHierarchy(
			IEnumerable<Link> links, Options options)
		{
			Dictionary<NodeName, DependencyItem> items = new Dictionary<NodeName, DependencyItem>();

			items[NodeName.Root] = new DependencyItem(options.SourceNode.Root.Name, false);

			foreach (Link link in links)
			{
				Node node = EndPoint(link, options.IsSource);

				if (!items.TryGetValue(node.Name, out DependencyItem item))
				{
					DependencyItem parentItem = GetParentItem(items, node.Parent);

					item = new DependencyItem(node.Name, node.HasCode);
					parentItem.AddChild(item);

					items[node.Name] = item;
				}
			}

			return items;
		}


		private DependencyItem GetParentItem(
			IDictionary<NodeName, DependencyItem> items,
			Node parentNode)
		{
			if (items.TryGetValue(parentNode.Name, out DependencyItem parentItem))
			{
				return parentItem;
			}

			parentItem = new DependencyItem(parentNode.Name, parentNode.HasCode);

			if (!parentNode.IsRoot)
			{
				DependencyItem grandParentItem = GetParentItem(items, parentNode.Parent);
				grandParentItem.AddChild(parentItem);
			}

			items[parentNode.Name] = parentItem;
			return parentItem;
		}


		private static Node EndPoint(IEdge edge, bool isSource)
		{
			return isSource ? edge.Source : edge.Target;
		}


		private class Options
		{
			public bool IsSource { get; }
			public Node SourceNode { get; }
			public Node TargetNode { get; }

			public Options(
				bool isSource,
				Node sourceNode,
				Node targetNode)
			{
				IsSource = isSource;
				SourceNode = sourceNode;
				TargetNode = targetNode;
			}




		}
	}
}