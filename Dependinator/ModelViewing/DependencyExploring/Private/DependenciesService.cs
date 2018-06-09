using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dependinator.ModelViewing.ModelHandling.Core;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependenciesService : IDependenciesService
	{
		public async Task<IReadOnlyList<DependencyItem>> GetDependencyItemsAsync(
			IEnumerable<Line> lines,
			bool isSource,
			Node sourceFilter,
			Node targetFilter)
		{
			return await Task.Run(() =>
			{
				ReferenceOptions options = new ReferenceOptions(isSource, sourceFilter, targetFilter);
				IEnumerable<Link> links = lines
					.SelectMany(line => line.Links)
					.Where(link => IsIncluded(link, options));

				var items = CreateReferenceHierarchy(links, options);

				DependencyItem rootItem = items[NodeName.Root];

				AddMainNodeIfNeeded(rootItem, options);

				return new List<DependencyItem> { rootItem };
			});
		}


		private void AddMainNodeIfNeeded(DependencyItem rootItem, ReferenceOptions options)
		{
			Node mainNode = options.IsSource ? options.SourceFilter : options.TargetFilter;

			if (rootItem.SubItems.Any() || mainNode.IsRoot)
			{
				return;
			}

			DependencyItem current = rootItem;

			foreach (Node node in mainNode.AncestorsAndSelf().Reverse())
			{
				DependencyItem subItem = new DependencyItem(node.Name, node.CodeText != null);
				current.AddChild(subItem);
				current = subItem;
			}
		}


		private static bool IsIncluded(IEdge link, ReferenceOptions options) =>
			(options.SourceFilter.IsRoot || link.Source.AncestorsAndSelf().Contains(options.SourceFilter)) &&
			(options.TargetFilter.IsRoot || link.Target.AncestorsAndSelf().Contains(options.TargetFilter)) &&
			!link.Target.AncestorsAndSelf().Any(n => n.View.IsHidden);



		private Dictionary<NodeName, DependencyItem> CreateReferenceHierarchy(
			IEnumerable<Link> links, ReferenceOptions options)
		{
			Dictionary<NodeName, DependencyItem> items = new Dictionary<NodeName, DependencyItem>();

			items[NodeName.Root] = new DependencyItem(
				options.SourceFilter.Root.Name, options.SourceFilter.Root.CodeText != null);

			foreach (Link link in links)
			{
				Node node = EndPoint(link, options.IsSource);

				if (!items.TryGetValue(node.Name, out DependencyItem item))
				{
					DependencyItem parentItem = GetParentItem(items, node.Parent);

					item = new DependencyItem(node.Name, node.CodeText != null);
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

			parentItem = new DependencyItem(parentNode.Name, parentNode.CodeText != null);

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


		private class ReferenceOptions
		{
			public bool IsSource { get; }
			public Node SourceFilter { get; }
			public Node TargetFilter { get; }


			public ReferenceOptions(
				bool isSource,
				Node sourceFilter,
				Node targetFilter)
			{
				IsSource = isSource;
				SourceFilter = sourceFilter;
				TargetFilter = targetFilter;
			}
		}
	}
}