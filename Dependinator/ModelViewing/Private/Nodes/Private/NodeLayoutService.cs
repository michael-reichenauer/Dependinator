﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.Private.ItemsViewing;
using Dependinator.ModelViewing.Private.ModelHandling.Core;
using Dependinator.ModelViewing.Private.ModelHandling.Private;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.Nodes.Private
{
    internal class NodeLayoutService : INodeLayoutService
    {
        private static readonly Size DefaultSize = new Size(200, 100);

        private readonly Layout[] layouts =
        {
            new Layout(1, 1, 50, 5, 40, 20),
            new Layout(2, 1, 50, 2, 125, 50),
            new Layout(4, 2, 50, 2, 125, 50),
            new Layout(12, 4, 100, 1, 150, 120),
            new Layout(int.MaxValue, 6, 50, 0.8, 150, 120)
            //new Layout(30, 6, 50, 0.8, 150, 120),
            //new Layout(int.MaxValue, 9, 50, 0.6, 60, 120),
        };

        private readonly IModelDatabase modelDatabase;


        public NodeLayoutService(IModelDatabase modelDatabase)
        {
            this.modelDatabase = modelDatabase;
        }


        public void SetLayout(NodeViewModel nodeViewModel)
        {
            if (!nodeViewModel.Node.Bounds.Same(RectEx.Zero))
            {
                nodeViewModel.SetBounds(nodeViewModel.Node.Bounds, false);
                nodeViewModel.Node.Parent.IsLayoutCompleted = true;
                return;
            }

            if (nodeViewModel.Node.Parent.IsLayoutCompleted)
            {
                AdjustLayout(nodeViewModel);
            }
            else
            {
                Node parent = nodeViewModel.Node.Parent;
                ResetLayoutImpl(parent);
            }
        }


        public void RearrangeLayout(Node node)
        {
            if (!node.Children.Any())
            {
                return;
            }

            Layout layout = GetLayout(node);

            SetScale(layout, node);

            int index = 0;
            IList<Node> children = node.Children.OrderBy(child => child, CompareNodes()).ToList();

            foreach (Node child in children)
            {
                Rect bounds = GetBounds(index++, layout);

                child.ViewModel.SetBounds(bounds, true);
            }

            node.Children.ForEach(child => modelDatabase.SetIsChanged(node));
        }



        private void ResetLayoutImpl(Node parent)
        {
            Layout layout = GetLayout(parent);

            SetScale(layout, parent);

            int index = 0;

            foreach (Node child in parent.Children)
            {
                Rect bounds = GetBounds(index++, layout);

                child.ViewModel.SetBounds(bounds, true);
            }
        }


        private void AdjustLayout(NodeViewModel nodeViewModel)
        {
            Node parent = nodeViewModel.Node.Parent;

            Layout layout = GetLayout(parent);


            int index = 0;

            while (true)
            {
                Rect bounds = GetBounds(index++, layout);

                if (!IsIntersecting(bounds, parent.Children))
                {
                    nodeViewModel.SetBounds(bounds, true);
                    break;
                }
            }
        }


        private void SetScale(Layout layout, Node parentNode)
        {
            // Adjust if scale if node has been resized
            double customFactor = DefaultSize.Width / (parentNode.ViewModel?.ItemBounds.Width ?? 1);
            double scaleFactor = layout.ScaleFactor / customFactor;

            if (!scaleFactor.Same(parentNode.ItemsCanvas.ScaleFactor))
            {
                // modelDatabase.SetIsChanged(parentNode);
                parentNode.ItemsCanvas.ScaleFactor = scaleFactor;
                parentNode.ItemsCanvas.UpdateScale();
            }
        }


        private static bool IsIntersecting(Rect bounds, IEnumerable<Node> children)
        {
            return children.Any(child => child.ViewModel.ItemBounds.IntersectsWith(bounds));
        }


        private Layout GetLayout(Node parent)
        {
            if (parent.IsRoot)
            {
                return layouts[1];
            }

            int itemCount = parent.Children.Count;

            return layouts.First(l => itemCount <= l.MaxItems);
        }


        private static Rect GetBounds(int siblingIndex, Layout layout)
        {
            Size size = DefaultSize;
            double x = siblingIndex % layout.RowLength * (size.Width + layout.Padding) + layout.XMargin;
            double y = siblingIndex / layout.RowLength * (size.Height + layout.Padding) + layout.YMargin;

            Point location = new Point(x, y).Rnd(5);

            return new Rect(location, size);
        }


        private static bool IsParentShowing(NodeViewModel nodeViewMode)
        {
            return nodeViewMode.Node.IsRoot
                   || nodeViewMode.Node.Parent.ViewModel.IsShowing;
        }


        private static IComparer<Node> CompareNodes() => Compare.With<Node>(CompareNodes);


        private static int CompareNodes(Node e1, Node e2)
        {
            if (e1 == e2)
            {
                return 0;
            }

            // Compare Parent to child references (links from outside nodes to child)
            // Child with more references from the outside is more to left/top
            Line parentToE1 = e1.Parent.SourceLines.FirstOrDefault(r => r.Target == e1);
            Line parentToE2 = e2.Parent.SourceLines.FirstOrDefault(r => r.Target == e2);

            int parentToE1Count = parentToE1?.LinkCount ?? 0;
            int parentToE2Count = parentToE2?.LinkCount ?? 0;

            if (parentToE1Count > parentToE2Count)
            {
                return -1;
            }

            if (parentToE1Count < parentToE2Count)
            {
                return 1;
            }

            // Compare siblings references
            // Sibling with more references to the other is more to left/top
            Line e1ToE2 = e1.SourceLines.FirstOrDefault(r => r.Target == e2);
            Line e2ToE1 = e2.SourceLines.FirstOrDefault(r => r.Target == e1);

            int e1ToE2Count = e1ToE2?.LinkCount ?? 0;
            int e2ToE1Count = e2ToE1?.LinkCount ?? 0;

            if (e1ToE2Count > e2ToE1Count)
            {
                return -1;
            }

            if (e1ToE2Count < e2ToE1Count)
            {
                return 1;
            }


            // Compare child to parent references (links from child to outside nodes)
            // Child with more references to outside is more to right/bottom
            Line e1ToParent = e1.SourceLines.FirstOrDefault(r => r.Target == e1.Parent);
            Line e2ToParent = e2.SourceLines.FirstOrDefault(r => r.Target == e2.Parent);

            int e1ToParentCount = e1ToParent?.LinkCount ?? 0;
            int e2ToParentCount = e2ToParent?.LinkCount ?? 0;

            if (e1ToParentCount > e2ToParentCount)
            {
                return 1;
            }

            if (e1ToParentCount < e2ToParentCount)
            {
                return -1;
            }


            return Txt.Compare(e1.Name.FullName, e2.Name.FullName);
        }


        private class Layout
        {
            public Layout(
                int maxItems,
                int rowLength,
                int padding,
                double relativeScaleFactor,
                double xMargin,
                double yMargin)
            {
                MaxItems = maxItems;
                RowLength = rowLength;
                Padding = padding;
                ScaleFactor = relativeScaleFactor * ItemsCanvas.DefaultScaleFactor;
                XMargin = xMargin;
                YMargin = yMargin;
            }


            public int MaxItems { get; }
            public int RowLength { get; }
            public int Padding { get; }
            public double ScaleFactor { get; }
            public double XMargin { get; }
            public double YMargin { get; }
        }
    }
}
