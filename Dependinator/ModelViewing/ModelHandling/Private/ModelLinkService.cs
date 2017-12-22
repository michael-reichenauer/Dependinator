using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.Common;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class ModelLinkService : IModelLinkService
	{
		private readonly ILinkSegmentService linkSegmentService;
		private readonly ILineViewModelService lineViewModelService;
		private readonly Lazy<IModelNodeService> modelNodeService;
		private readonly Model model;


		public ModelLinkService(
			ILinkSegmentService linkSegmentService,
			ILineViewModelService lineViewModelService,
			Lazy<IModelNodeService> modelNodeService,
			Model model)
		{
			this.linkSegmentService = linkSegmentService;
			this.lineViewModelService = lineViewModelService;
			this.modelNodeService = modelNodeService;
			this.model = model;
		}


		public void UpdateLink(ModelLink modelLink, int stamp)
		{
			try
			{
				Node source = model.Node(NodeName.From(modelLink.Source));

				if (!TryGetTarget(modelLink, out Node target))
				{
					return;
				}


				target.Stamp = stamp;

				if (TryGetLink(source, target, out Link link))
				{
					// Already added link
					link.Stamp = stamp;
					return;
				}

				link = AddLink(source, target);
				link.Stamp = stamp;
				var linkSegments = linkSegmentService.GetLinkSegments(link);

				AddLinkSegmentLines(linkSegments, link);
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed to update link {modelLink}");
				throw;
			}
		}


		public void UpdateLine(ModelLine modelLine, int stamp)
		{
			try
			{
				Node source = model.Node(NodeName.From(modelLine.Source));

				if (!TryGetTarget(modelLine, out Node target))
				{
					return;
				}

				//Node target = model.Node(NodeName.From(modelLine.Target));

				if (TryGetLine(source, target, out Line line))
				{
					// Already added link
					line.Stamp = stamp;
					return;
				}

				AddLine(source, target, modelLine.LinkCount, modelLine.Points);
			}
			catch (Exception e)
			{
				Log.Exception(e, $"Failed to update link {modelLine}");
				throw;
			}
		}


		private bool TryGetTarget(ModelLink modelLink, out Node target)
		{
			NodeName targetName = NodeName.From(modelLink.Target);
			if (!model.TryGetNode(targetName, out target))
			{
				model.QueueModelLink(targetName, modelLink);
				return false;
			}

			return true;
		}

		private bool TryGetTarget(ModelLine modelLine, out Node target)
		{
			NodeName targetName = NodeName.From(modelLine.Target);
			if (!model.TryGetNode(targetName, out target))
			{
				model.QueueModelLine(targetName, modelLine);
				return false;
			}

			return true;
		}


		public void RemoveObsoleteLinks(IReadOnlyList<Link> obsoleteLinks)
		{
			foreach (Link link in obsoleteLinks)
			{
				foreach (Line line in link.Lines)
				{
					line.Links.Remove(link);

					if (!line.Links.Any())
					{
						RemoveLine(line);
					}
				}

				RemoveLink(link);
			}
		}


		public void ResetLayout(List<Link> links)
		{
			foreach (Link link in links)
			{
				foreach (Line line in link.Lines)
				{
					if (line.Points.Count != 2)
					{
						line.ResetPoints();
						line.ViewModel.UpdateLine();
						line.ViewModel.NotifyAll();
					}
				}
			}
		}



		private static Link AddLink(Node source, Node target)
		{
			Link link = new Link(source, target);
			link.Source.SourceLinks.Add(link);
			return link;
		}

		private void RemoveLink(Link link)
		{
			link.Source.SourceLinks.Remove(link);
		}


		private void AddLinkSegmentLines(IEnumerable<LinkSegment> linkSegments, Link link)
		{
			foreach (LinkSegment linkSegment in linkSegments)
			{
				if (!TryGetLine(linkSegment, out Line line))
				{
					line = AddLine(linkSegment.Source, linkSegment.Target, 0, null);
				}

				line.Links.Add(link);
				link.Lines.Add(line);
			}
		}


		private Line AddLine(Node source, Node target, int linkCount, IReadOnlyList<Point> points)
		{
			Node owner = GetLineOwner(source, target);
			Line line = new Line(source, target, owner);

			if (points != null)
			{
				line.Points.InsertRange(1, points);
			}

			line.Source.SourceLines.Add(line);
			line.Target.TargetLines.Add(line);
			line.LinkCount = linkCount;


			if (owner.View.IsShowing || line.Source.View.IsShowing || line.Target.View.IsShowing)
			{
				AddLineViewModel(line);
			}

			return line;
		}


		private void RemoveLine(Line line)
		{
			line.Source.SourceLines.Remove(line);
			line.Target.TargetLines.Remove(line);
			RemoveLineViewModel(line);
		}



		public void AddLineViewModel(Line line)
		{
			LineViewModel lineViewModel = new LineViewModel(lineViewModelService, line);

			line.Owner.View.ItemsCanvas.AddItem(lineViewModel);
		}


		private void RemoveLineViewModel(Line line)
		{
			if (line.ViewModel != null)
			{
				line.Owner.View.ItemsCanvas.RemoveItem(line.ViewModel);
			}
		}


		private static Node GetLineOwner(Node source, Node target) =>
			source == target.Parent ? source : source.Parent;


		//private bool TryGetNode(NodeName nodeName, out Node node)
		//{
		//	if (model.TryGetNode(nodeName, out node))
		//	{
		//		return true;
		//	}

		//	return node;
		//}


		private static bool TryGetLine(LinkSegment segment, out Line line)
		{
			line = segment.Source.SourceLines
				.FirstOrDefault(l => l.Source == segment.Source && l.Target == segment.Target);
			return line != null;
		}


		private static bool TryGetLink(Node source, Node target, out Link link)
		{
			link = source.SourceLinks.FirstOrDefault(l => l.Source == source && l.Target == target);
			return link != null;
		}


		private static bool TryGetLine(Node source, Node target, out Line line)
		{
			line = source.SourceLines.FirstOrDefault(l => l.Source == source && l.Target == target);
			return line != null;
		}

	}
}