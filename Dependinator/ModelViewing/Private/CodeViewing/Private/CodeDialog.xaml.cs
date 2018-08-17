using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using Dependinator.Common;
using Dependinator.Common.ProgressHandling;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
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
        private Source source;
        private readonly Func<Task<M<Source>>> updateSource;
        private readonly Lazy<IModelNotifications> modelNotifications;

        
        internal CodeDialog(
            Lazy<IModelNotifications> modelNotifications,
            ISolutionService solutionService,
            IProgressService progressService,
            WindowOwner owner,
            NodeName nodeName,
            Source source,
            Func<Task<M<Source>>> updateSource)
        {
            this.modelNotifications = modelNotifications;

            this.source = source;
            this.updateSource = updateSource;

            Owner = owner;
            InitializeComponent();

            codeViewModel = new CodeViewModel(solutionService, progressService, nodeName.DisplayLongName, this);
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


        private async void OnModelUpdated(object sender, EventArgs e)
        {
            M<Source> result = await updateSource();
            if (result.IsOk)
            {
                source = result.Value;
            }

            SetCodeText();
        }


        private async void SetCodeText()
        {
            CodeView.Options.IndentationSize = 4;
            CodeView.Text = source.Text;

            // Await code to be rendered before scrolling to line number
            await Task.Yield();
            codeViewModel.FilePath = source.Path;
            codeViewModel.LineNumber = source.LineNumber;
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
