using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Common.ModelMetadataFolders.Private;


namespace Dependinator.Common.ModelMetadataFolders
{
    /// <summary>
    ///     Interaction logic for OpenModelView.xaml
    /// </summary>
    public partial class OpenModelView : UserControl
    {
        //private MouseClicked mouseClicked;


        public OpenModelView()
        {
            InitializeComponent();

            //mouseClicked = new MouseClicked(this, Clicked);
        }


        private OpenModelViewModel ViewModel => DataContext as OpenModelViewModel;


        private void RecentFile_OnClick(object sender, MouseButtonEventArgs e)
        {
            ((sender as FrameworkElement)?.DataContext as FileItem)?.OpenFileCommand.Execute();
        }


        private void OpenFile_OnClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.OpenFile();
        }


        private void OpenExample_OnClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel?.OpenExampleFile();
        }
    }
}
