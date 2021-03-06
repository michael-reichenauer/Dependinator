﻿using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.Private.ModelHandling.Core;


namespace Dependinator.ModelViewing.Private.Nodes
{
    internal interface INodeViewModelService
    {
        Brush GetBackgroundBrush(Brush brush);

        Brush GetNodeBrush(Node node);
        void FirstShowNode(Node node);

        void MouseClicked(NodeViewModel nodeViewModel);
        void OnMouseWheel(NodeViewModel nodeViewModel, UIElement uiElement, MouseWheelEventArgs e);
        Brush GetSelectedBrush(Brush brush);
        void ShowReferences(NodeViewModel nodeViewModel);
        Task ShowCodeAsync(Node node);
        void RearrangeLayout(NodeViewModel node);
        void HideNode(Node node);
        Brush GetDimBrush();
        Brush GetTitleBrush();
        void ShowNode(Node node);
        void SetIsChanged(Node node);
    }
}
