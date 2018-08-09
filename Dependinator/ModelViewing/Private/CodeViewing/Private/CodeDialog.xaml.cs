using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using Dependinator.Common;
using Dependinator.Common.MessageDialogs;
using Dependinator.ModelViewing.Private.ModelHandling;
using Dependinator.Utils.ErrorHandling;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;


namespace Dependinator.ModelViewing.Private.CodeViewing.Private
{
    /// <summary>
    ///     Interaction logic for ReferencesDialog.xaml
    /// </summary>
    public partial class CodeDialog : Window
    {
        private readonly CodeViewModel codeViewModel;
        private readonly Func<NodeName, Task<M<SourceCode>>> getCodeActionAsync;
        private readonly IMessage message;
        private readonly Lazy<IModelNotifications> modelNotifications;

        private readonly NodeName nodeName;


        internal CodeDialog(
            Lazy<IModelNotifications> modelNotifications,
            IMessage message,
            ISolutionService solutionService,
            WindowOwner owner,
            NodeName nodeName,
            Func<NodeName, Task<M<SourceCode>>> getCodeActionAsync)
        {
            this.modelNotifications = modelNotifications;
            this.message = message;

            this.nodeName = nodeName;
            this.getCodeActionAsync = getCodeActionAsync;
            Owner = owner;
            InitializeComponent();

            codeViewModel = new CodeViewModel(solutionService, nodeName.DisplayLongName, this);
            DataContext = codeViewModel;

            SetSyntaxHighlighting();

            SetCodeText();

            modelNotifications.Value.ModelUpdated += OnModelUpdated;
        }


        protected override void OnClosing(CancelEventArgs e)
        {
            modelNotifications.Value.ModelUpdated -= OnModelUpdated;

            base.OnClosing(e);
        }


        private void OnModelUpdated(object sender, EventArgs e) => SetCodeText();


        private async void SetCodeText()
        {
            CodeView.Text = "Getting code ...";

            M<SourceCode> codeResult = await getCodeActionAsync(nodeName);

            if (codeResult.IsFaulted)
            {
                message.ShowWarning(codeResult.Message);
                Close();
                return;
            }

            CodeView.Options.IndentationSize = 2;
            CodeView.Text = codeResult.Value.Text;

            // Await code to be rendered before scrolling to line number
            await Task.Yield();
            codeViewModel.FilePath = codeResult.Value.FilePath;
            codeViewModel.LineNumber = codeResult.Value.LineNumber;
            CodeView.ScrollTo(codeViewModel.LineNumber, 0);
        }


        private void SetSyntaxHighlighting()
        {
            Assembly programAssembly = ProgramInfo.Assembly;
            string name = programAssembly.FullName.Split(',')[0];
            string resourceName = $"{name}.Common.Resources.CSharp-Mode.xshd";

            using (Stream stream = programAssembly.GetManifestResourceStream(resourceName))
            using (XmlTextReader reader = new XmlTextReader(stream))
            {
                CodeView.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
        }
    }
}
