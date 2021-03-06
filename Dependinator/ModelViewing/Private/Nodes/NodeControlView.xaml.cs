﻿using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Private.Nodes
{
    /// <summary>
    ///     Interaction logic for NodeControlView.xaml
    /// </summary>
    public partial class NodeControlView : UserControl
    {
        private readonly Dictionary<string, NodeControl> points;
        private MouseClicked mouseClicked;


        public NodeControlView()
        {
            InitializeComponent();

            mouseClicked = new MouseClicked(ControlCenter, Clicked);

            points = new Dictionary<string, NodeControl>
            {
                {ControlCenter.Name, NodeControl.Center},
                {ControlLeftTop.Name, NodeControl.LeftTop},
                {ControlLeftBottom.Name, NodeControl.LeftBottom},
                {ControlRightTop.Name, NodeControl.RightTop},
                {ControlRightBottom.Name, NodeControl.RightBottom},

                {ControlTop.Name, NodeControl.Top},
                {ControlLeft.Name, NodeControl.Left},
                {ControlRight.Name, NodeControl.Right},
                {ControlBottom.Name, NodeControl.Bottom}
            };
        }


        private NodeControlViewModel ViewModel => DataContext as NodeControlViewModel;


        private void Clicked(MouseButtonEventArgs e) => ViewModel.Clicked(e);


        protected override void OnMouseWheel(MouseWheelEventArgs e) =>
            ViewModel.OnMouseWheel(this, e);


        private void Control_OnMouseMove(object sender, MouseEventArgs e) =>
            MouseHelper.OnLeftButtonMove((UIElement)sender, e, Move);


        private void Move(UIElement element, Vector offset, Point p1, Point p2)
        {
            Rectangle rectangle = (Rectangle)element;
            NodeControl control = points[rectangle.Name];
            ViewModel?.Move(control, offset, p1, p2);
        }


        private void HamburgerButton_OnClick(object sender, RoutedEventArgs e)
        {
            HamburgerContextMenu.PlacementTarget = this;
            HamburgerContextMenu.IsOpen = true;
        }
    }
}
