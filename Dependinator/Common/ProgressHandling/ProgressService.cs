using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Dependinator.Utils;
using Dependinator.Utils.Threading;


namespace Dependinator.Common.ProgressHandling
{
    internal class ProgressService : IProgressService
    {
        private readonly Lazy<IBusyIndicatorProvider> mainWindowViewModel;
        private readonly WindowOwner windowOwner;
        private Progress currentProgress;


        public ProgressService(
            WindowOwner owner,
            Lazy<IBusyIndicatorProvider> mainWindowViewModel)
        {
            this.windowOwner = owner;
            this.mainWindowViewModel = mainWindowViewModel;
        }


        public IDisposable ShowBusy()
        {
            return mainWindowViewModel.Value.Busy.Progress();
        }


        public void SetText(string text)
        {
            currentProgress?.SetText(text);
        }


        public Progress ShowDialog(string text = "", Window owner = null)
        {
            Log.Debug($"Progress status: {text}");

            ProgressBox progress = new ProgressBox(owner ?? windowOwner, text);

            progress.StartShowDialog();
            currentProgress = progress;
            return progress;
        }


        internal class ProgressBox : Progress
        {
            private readonly TaskCompletionSource<bool> closeTask = new TaskCompletionSource<bool>();
            private readonly ProgressDialog progressDialog;
            private readonly Timing timing;


            public ProgressBox(Window owner, string text)
            {
                timing = new Timing();
                timing.Log($"Progress status: {text}");
                progressDialog = new ProgressDialog(owner, text, closeTask.Task);
            }


            public override void Dispose()
            {
                timing.Log("Progress status done");
                closeTask.TrySetResult(true);
            }


            public override void SetText(string text)
            {
                timing.Log($"Progress status: {text}");
                progressDialog.SetText(text);
            }


            public void StartShowDialog()
            {
                SynchronizationContext.Current.Post(_ => progressDialog.ShowDialog(), null);
            }
        }
    }
}
