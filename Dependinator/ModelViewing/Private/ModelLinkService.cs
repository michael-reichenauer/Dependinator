using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelParsing;
using Dependinator.ModelViewing.Links;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	internal class ModelLinkService : IModelLinkService
	{
		private readonly ILinkSegmentService linkSegmentService;
		private readonly ILineViewModelService lineViewModelService;
		private readonly Model model;


		public ModelLinkService(
			ILinkSegmentService linkSegmentService,
			ILineViewModelService lineViewModelService,
			Model model)
		{
			this.linkSegmentService = linkSegmentService;
			this.lineViewModelService = lineViewModelService;
			this.model = model;
		}


		public void UpdateLink(ModelLink modelLink, int stamp)
		{
			if (modelLink.Source == modelLink.Target)
			{
				// Skipping link to self for now
				return;
			}

			Node source = model.Node(modelLink.Source);
			Node target = model.Node(modelLink.Target);
		
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
					line = AddLine(linkSegment);				
				}

				line.Links.Add(link);
				link.Lines.Add(line);
			}
		}


		private Line AddLine(LinkSegment segment)
		{
			Line line = new Line(segment.Source, segment.Target);
			line.Source.SourceLines.Add(line);
			line.Target.TargetLines.Add(line);
			AddLineViewModel(line);
			return line;
		}


		private void RemoveLine(Line line)
		{
			line.Source.SourceLines.Remove(line);
			line.Target.TargetLines.Remove(line);
			RemoveLineViewModel(line);
		}

		

		public void InitLine(Line line)
		{
			//Node owner = GetLineOwner(line);

			//LineViewModel lineViewModel = new LineViewModel(lineViewModelService, line);

			//owner.ItemsCanvas.AddItem(lineViewModel);
		}


		private void AddLineViewModel(Line line)
		{
			Node owner = GetLineOwner(line);

			//if (line.Source.IsShowing || line.Target.IsShowing) 
			{
				LineViewModel lineViewModel = new LineViewModel(lineViewModelService, line);

				owner.ItemsCanvas.AddItem(lineViewModel);
			}
		}

		private void RemoveLineViewModel(Line line)
		{
			if (line.ViewModel != null)
			{
				Node owner = GetLineOwner(line);
				owner.ItemsCanvas.RemoveItem(line.ViewModel);
			}
		}


		private static Node GetLineOwner(Line line) => 
			line.Source == line.Target.Parent ? line.Source : line.Source.Parent;


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
	}
}