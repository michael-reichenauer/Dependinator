using System;
using System.ComponentModel.Design;
using System.Threading;
using DependinatorVse.Commands.Private;
using DependinatorVse.Utils;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Process = System.Diagnostics.Process;
using Task = System.Threading.Tasks.Task;


namespace DependinatorVse.Commands
{
    /// <summary>
    ///     Command handler for show code node in Dependinator exe.
    /// </summary>
    internal sealed class ShowInDependinatorCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("3503f324-db31-4488-9db2-c484cbd1609c");

        private readonly AsyncPackage package;


        private ShowInDependinatorCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }


        public static ShowInDependinatorCommand Instance { get; private set; }


        private IAsyncServiceProvider ServiceProvider => package;


        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Verify the current thread is the UI thread - the call to AddCommand in ShowInDependinatorCommand's constructor requires
            // the UI thread.
            await package.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

            OleMenuCommandService commandService =
                await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new ShowInDependinatorCommand(package, commandService);
        }


        /// <summary>
        ///     Shows the node corresponding to the active text code in Dependinator
        /// </summary>
        private async void Execute(object sender, EventArgs e)
        {
            try
            {
                await package.JoinableTaskFactory.SwitchToMainThreadAsync();

                DTE2 dte = await Studio.GetDteAsync(package);

                Solution solution = dte.Solution;
                if (solution == null || string.IsNullOrEmpty(solution.FullName))
                {
                    Studio.ShowInfoMessageBox(package, "No solution has been loaded yet.");
                    return;
                }

                Document document = dte.ActiveDocument;
                if (document == null)
                {
                    Studio.ShowInfoMessageBox(package, "Open a file and set the cursor within it first.");
                    return;
                }

                TextSelection selection = (TextSelection)document.Selection;

                DependinatorApiClient apiClient = new DependinatorApiClient(
                    solution.FileName, Studio.IsDeveloperMode(dte));

                if (!apiClient.IsDependinatorInstalled)
                {
                    if (Studio.ShowInfoMessageBox(package,
                        "The Dependinator application is needed to visualize the solution.\n\n" +
                        "Please install the latest release.", true))
                    {
                        OpenLatestReleasePage();
                    }

                    return;
                }

                await apiClient.ShowFileAsync(document.FullName, selection.CurrentLine);
            }
            catch (Exception exception)
            {
                Studio.ShowWarnMessageBox(package,
                    "Really sorry, the node could not be shown.\n" +
                    $"Error: {exception.Message}\n" +
                    "Please report the issue at:\n" +
                    "https://github.com/michael-reichenauer/Dependinator/issues");

                Log.Error($"Failed: {exception}");
            }
        }


        private static void OpenLatestReleasePage()
        {
            try
            {
                Process process = new Process();
                process.StartInfo.FileName = "https://github.com/michael-reichenauer/Dependinator/releases/latest";
                process.Start();
            }
            catch (Exception e)
            {
                Log.Error($"Failed to open latest release link, {e}");
            }
        }
    }
}
