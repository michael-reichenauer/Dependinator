﻿using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal class ModelLinkService : IModelLinkService
    {
        private readonly IModelLineService modelLineService;
        private readonly IModelDatabase modelService;


        public ModelLinkService(
            IModelLineService modelLineService,
            IModelDatabase modelService)
        {
            this.modelLineService = modelLineService;
            this.modelService = modelService;
        }


        public void AddOrUpdateLink(DataLink dataLink, int stamp)
        {
            try
            {
                Node source = modelService.GetNode(dataLink.Source);

                if (!TryGetTarget(dataLink, out Node target))
                {
                    modelService.QueueNode(new DataNode(dataLink.Target, null, dataLink.TargetType));
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

                if (!dataLink.IsAdded)
                {
                    AddLinkToLines(link);
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, $"Failed to update link {dataLink}");
                throw;
            }
        }


        public void RemoveObsoleteLinks(IReadOnlyList<Link> obsoleteLinks)
        {
            foreach (Link link in obsoleteLinks)
            {
                RemoveLinkFromLines(link);

                RemoveLink(link);
            }
        }


        public void Hide(Link link) => RemoveLinkFromLines(link);


        public void Show(Link link) => AddLinkToLines(link);


        private bool TryGetTarget(DataLink dataLink, out Node target)
        {
            NodeName targetName = dataLink.Target;
            if (!modelService.TryGetNode(targetName, out target))
            {
                modelService.QueueModelLink(targetName, dataLink);
                return false;
            }

            return true;
        }


        private void AddLinkToLines(Link link)
        {
            if (!link.Source.IsHidden && !link.Target.IsHidden)
            {
                modelLineService.AddLinkLines(link);
            }
        }


        private void RemoveLinkFromLines(Link link)
        {
            foreach (LinkSegment segment in modelLineService.GetLinkSegments(link))
            {
                Line line = segment.Source.SourceLines.FirstOrDefault(
                    l => l.Source == segment.Source && l.Target == segment.Target);

                if (line != null)
                {
                    line.LinkCount--;

                    if (line.LinkCount <= 0)
                    {
                        modelLineService.RemoveLine(line);
                    }
                }
            }
        }


        private static Link AddLink(Node source, Node target)
        {
            Link link = new Link(source, target);
            link.Source.SourceLinks.Add(link);
            link.Target.TargetLinks.Add(link);

            return link;
        }


        private void RemoveLink(Link link)
        {
            link.Source.SourceLinks.Remove(link);
            link.Target.TargetLinks.Remove(link);
        }


        private static bool TryGetLink(Node source, Node target, out Link link)
        {
            link = source.SourceLinks.FirstOrDefault(l => l.Source == source && l.Target == target);
            return link != null;
        }
    }
}
