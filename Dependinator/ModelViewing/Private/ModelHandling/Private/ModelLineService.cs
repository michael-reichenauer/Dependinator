using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.Lines;
using Dependinator.ModelViewing.Private.Lines.Private;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal class ModelLineService : IModelLineService
    {
        private readonly ILineViewModelService lineViewModelService;
        private readonly ILinkSegmentService linkSegmentService;
        private readonly IModelDatabase modelDatabase;


        public ModelLineService(
            ILinkSegmentService linkSegmentService,
            ILineViewModelService lineViewModelService,
            IModelDatabase modelDatabase)
        {
            this.linkSegmentService = linkSegmentService;
            this.lineViewModelService = lineViewModelService;
            this.modelDatabase = modelDatabase;
        }


        public void AddOrUpdateLine(DataLine dataLine, int stamp)
        {
            try
            {
                Node source = modelDatabase.GetNode(dataLine.Source);

                if (!TryGetTarget(dataLine, out Node target))
                {
                    return;
                }

                if (TryGetLine(source, target, out Line line))
                {
                    // Already added link
                    line.Stamp = stamp;

                    if (dataLine.Points == null)
                    {
                        if (modelDatabase.TryGetSavedLine(
                            new LineId(source.Name, target.Name), out DataLine savedLine))
                        {
                            line.View.Points.InsertRange(1, savedLine.Points);
                        }
                    }

                    return;
                }

                AddLine(source, target, dataLine.LinkCount, dataLine.Points);
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Failed to update link {dataLine}");
                throw;
            }
        }


        public void UpdateLines(Node node)
        {
            node.Root.Descendents().SelectMany(n => n.SourceLines).ForEach(line => line.LinkCount = 0);

            node.Root.Descendents().SelectMany(n => n.SourceLinks).ForEach(link => ShowLinkIfNeeded(link));

            node.Root.Descendents().SelectMany(n => n.SourceLines).ToList().ForEach(line => RemoveLineIfNoLinks(line));
        }


        public IEnumerable<LinkSegment> GetLinkSegments(Link link) =>
            linkSegmentService.GetLinkSegments(link);


        public void AddLinkLines(Link link)
        {
            IEnumerable<LinkSegment> linkSegments = linkSegmentService.GetLinkSegments(link);
            AddLinkSegmentLines(linkSegments);
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

            line.Owner.ItemsCanvas.AddItem(lineViewModel);
        }


        private void RemoveLineIfNoLinks(Line line)
        {
            if (line.LinkCount <= 0)
            {
                RemoveLine(line);
            }
        }


        private void ShowLinkIfNeeded(Link link)
        {
            if (!link.Source.IsHidden && !link.Target.IsHidden)
            {
                AddLinkToLines(link);
            }
        }


        private void AddLinkToLines(Link link)
        {
            if (!link.Source.IsHidden && !link.Target.IsHidden)
            {
                AddLinkLines(link);
            }
        }


        private void AddLinkSegmentLines(IEnumerable<LinkSegment> linkSegments)
        {
            foreach (LinkSegment linkSegment in linkSegments)
            {
                if (!TryGetLine(linkSegment, out Line line))
                {
                    line = AddLine(linkSegment.Source, linkSegment.Target, 0, null);
                }

                line.LinkCount++;
            }
        }


        private Line AddLine(Node source, Node target, int linkCount, IReadOnlyList<Point> points)
        {
            Line line = new Line(source, target);

            if (points != null)
            {
                line.View.Points.InsertRange(1, points);
            }
            else
            {
                if (modelDatabase.TryGetSavedLine(
                    new LineId(source.Name, target.Name), out DataLine dataLine))
                {
                    line.View.Points.InsertRange(1, dataLine.Points);
                }
            }

            line.Source.SourceLines.Add(line);
            line.Target.TargetLines.Add(line);
            line.LinkCount = linkCount;


            if (line.Owner.IsShowing || line.Source.IsShowing || line.Target.IsShowing)
            {
                AddLineViewModel(line);
            }

            return line;
        }


        private void RemoveLineViewModel(Line line)
        {
            if (line.View.ViewModel != null)
            {
                line.Owner.ItemsCanvas.RemoveItem(line.View.ViewModel);
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


        private bool TryGetTarget(DataLine dataLine, out Node target)
        {
            NodeName targetName = dataLine.Target;
            if (!modelDatabase.TryGetNode(targetName, out target))
            {
                modelDatabase.QueueModelLine(targetName, dataLine);
                return false;
            }

            return true;
        }
    }
}
