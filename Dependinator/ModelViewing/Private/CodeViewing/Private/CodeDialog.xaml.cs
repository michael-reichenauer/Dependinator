using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using Dependinator.Common;
using Dependinator.Common.ModelMetadataFolders;
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
            ModelMetadata modelMetadata,
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
            Loaded += Window_Loaded;

            codeViewModel = new CodeViewModel(solutionService, progressService, modelMetadata.ModelPaths, nodeName.DisplayLongName, this);
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


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Try to ensure that multiple windows are not at exactly same position (hiding lower)
            Random random = new Random();
            Left = Math.Max(10, Left + random.Next(-100, 100));
            Top = Math.Max(10, Top + random.Next(-100, 100));
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
