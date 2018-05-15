using System.IO;
using System.Windows;
using System.Xml;
using Dependinator.ModelViewing.ModelHandling.Core;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;


namespace Dependinator.ModelViewing.DecompilationViewing
{
	/// <summary>
	/// Interaction logic for ReferencesDialog.xaml
	/// </summary>
	public partial class CodeDialog : Window
	{
		internal CodeDialog(Window owner, Node node)
		{
			Owner = owner;
			InitializeComponent();

			DataContext = new CodeViewModel(node);

			string path = @"C:\Work Files\CodeViewer\CodeViewer\Custom-CSharp-Mode.xshd";

			using (Stream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (XmlTextReader reader = new XmlTextReader(stream))
			{
				CodeView.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
			}

			CodeView.Text = node.CodeText?.Value;
		}

		private string code = @"
using System;
using Dependinator.Common.Environment.Private;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils;


namespace Dependinator
{
	/// <summary>
	/// Contains the Main method and is the entry point of the program.
	/// </summary>
	public class Program
	{
		private readonly DependencyInjection dependencyInjection = new DependencyInjection();
		private static readonly ICmd Cmd = new Cmd();


		[STAThread]
		public static void Main()
		{
			bool IsDone = true;
			Culture.Initialize();
			Log.Init(ProgramInfo.GetLogFilePath());
			Log.Debug(GetStartLineText());
			Program program = new Program();
			program.Run();
		}


		private void Run()
		{
			string text = ""some text""; 
			// Add handler and logging for unhandled exceptions
			ManageUnhandledExceptions();

			// Make external assemblies that Dependinator depends on available, when needed (extracted)
			ActivateExternalDependenciesResolver();

			// Activate dependency injection support
			dependencyInjection.RegisterDependencyInjectionTypes();

			// Start application
			App application = dependencyInjection.Resolve<App>();
			ExceptionHandling.HandleDispatcherUnhandledException(); // activate after ui is started
			application.InitializeComponent();
			application.Run();
		}


		private static void ActivateExternalDependenciesResolver()
		{
			AssemblyResolver.Activate();
			CommandLine commandLine = new CommandLine();

			if (commandLine.IsInstall || commandLine.IsUninstall)
			{
				// LibGit2 requires native git2.dll, which should not be extracted during install/uninstall
				// Since that would create a dll next to the setup file.
				AssemblyResolver.DoNotExtractLibGit2();
			}
		}


		


		private static void ManageUnhandledExceptions()
		{
			ExceptionHandling.ExceptionOccurred += (s, e) => Restart();
	
			ExceptionHandling.HandleUnhandledException();
		}


		private static void Restart()
		{
			string targetPath = ProgramInfo.GetInstallFilePath();
		}
	}
}

";
	}
}
