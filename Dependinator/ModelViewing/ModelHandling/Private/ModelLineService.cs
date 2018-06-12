using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.Lines;
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ModelHandling.Private
{
	internal class ModelLineService : IModelLineService
	{
		private readonly ILinkSegmentService linkSegmentService;
		private readonly ILineViewModelService lineViewModelService;
		private readonly IModelService modelService;


		public ModelLineService(
			ILinkSegmentService linkSegmentService,
			ILineViewModelService lineViewModelService,
			IModelService modelService)
		{
			this.linkSegmentService = linkSegmentService;
			this.lineViewModelService = lineViewModelService;
			this.modelService = modelService;
		}



		public void UpdateLine(ModelLine modelLine, int stamp)
		{
			try
			{
				Node source = modelService.GetNode(NodeName.From(modelLine.Source));

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


		public void AddLinkLines(Link link)
		{
			IReadOnlyList<LinkSegment> linkSegments = linkSegmentService.GetLinkSegments(link);
			AddLinkSegmentLines(linkSegments, link);
		}


		public void RemoveLine(Line line)
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
			Line line = new Line(source, target);

			if (points != null)
			{
				line.View.Points.InsertRange(1, points);
			}

			line.Source.SourceLines.Add(line);
			line.Target.TargetLines.Add(line);
			line.LinkCount = linkCount;


			if (line.Owner.View.IsShowing || line.Source.View.IsShowing || line.Target.View.IsShowing)
			{
				AddLineViewModel(line);
			}

			return line;
		}


		private void RemoveLineViewModel(Line line)
		{
			if (line.View.ViewModel != null)
			{
				line.Owner.View.ItemsCanvas.RemoveItem(line.View.ViewModel);
			}
		}


		//private static Node GetLineOwner(Node source, Node target) =>
		//	source == target.Parent ? source : source.Parent;


		private static bool TryGetLine(LinkSegment segment, out Line line)
		{
			line = segment.Source.SourceLines
				.FirstOrDefault(l => l.Source == segment.Source && l.Target == segment.Target);
			return line != null;
		}


		private static bool TryGetLine(Node source, Node target, out Line line)
		{
			line = source.SourceLines.FirstOrDefault(l => l.Source == source && l.Target == target);
			return line != null;
		}

		private bool TryGetTarget(ModelLine modelLine, out Node target)
		{
			NodeName targetName = NodeName.From(modelLine.Target);
			if (!modelService.TryGetNode(targetName, out target))
			{
				modelService.QueueModelLine(targetName, modelLine);
				return false;
			}

			return true;
		}
	}
}