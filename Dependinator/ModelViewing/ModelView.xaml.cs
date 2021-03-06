﻿using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing
{
    /// <summary>
    ///     Interaction logic for ModelView.xaml
    /// </summary>
    public partial class ModelView : UserControl
    {
        private MouseClicked mouseClicked;


        public ModelView()
        {
            InitializeComponent();
            mouseClicked = new MouseClicked(this, e => ViewModel?.MouseClicked(e));
        }


        private ModelViewModel ViewModel => DataContext as ModelViewModel;


        private async void ModelView_OnLoaded(object sender, RoutedEventArgs e)
        {
            ItemsView.SetFocus();

            await ViewModel.OpenAsync();
        }


        private async void Dropped_Files(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            string[] filePaths = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (filePaths?.Any() ?? false)
            {
                await ViewModel.OpenFilesAsync(filePaths);
            }
        }


        protected override void OnMouseWheel(MouseWheelEventArgs e) => ViewModel.OnMouseWheel(this, e);
    }
}
