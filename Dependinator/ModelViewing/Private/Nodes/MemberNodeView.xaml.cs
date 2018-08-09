using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Private.Nodes
{
    /// <summary>
    ///     Interaction logic for MemberNodeView.xaml
    /// </summary>
    public partial class MemberNodeView : UserControl
    {
        private MouseClicked mouseClicked;


        public MemberNodeView()
        {
            InitializeComponent();
            mouseClicked = new MouseClicked(this, e => ViewModel?.MouseClicked(e));
        }


        private NodeViewModel ViewModel => DataContext as NodeViewModel;

        protected override void OnMouseWheel(MouseWheelEventArgs e) => ViewModel.OnMouseWheel(this, e);
    }
}
