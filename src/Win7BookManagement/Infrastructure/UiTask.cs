using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Win7BookManagement.Infrastructure
{
    /// <summary>A responsive modal work window. Business work never touches controls.</summary>
    public static class UiTask
    {
        public static void Run<T>(IWin32Window owner, string title,
            Func<IProgress<int>, CancellationToken, T> work, Action<T> completed,
            bool canCancel = true)
        {
            RunCore(owner, title, work, completed,
                () => MessageBox.Show(owner, "操作已取消。未完成的临时文件不会作为有效结果发布。", "已取消",
                    MessageBoxButtons.OK, MessageBoxIcon.Information),
                failure => CrashReporter.Handle(owner, failure, title), canCancel);
        }

        // Separate outcome notification from the modal work lifecycle so that
        // all paths can be exercised without blocking automated tests on a popup.
        private static void RunCore<T>(IWin32Window owner, string title,
            Func<IProgress<int>, CancellationToken, T> work, Action<T> completed,
            Action onCancelled, Action<Exception> onFailure, bool canCancel)
        {
            T result = default(T);
            Exception failure = null;
            var cancelled = false;
            using (var dialog = new ProgressWindow(title, canCancel))
            {
                EventHandler shown = null;
                shown = async delegate
                {
                    dialog.Shown -= shown;
                    try
                    {
                        // Capture the WinForms synchronization context only after
                        // the modal window is shown and its UI thread is ready.
                        dialog.InitializeProgress();
                        result = await Task.Run(() => work(dialog.Progress, dialog.Token)).ConfigureAwait(true);
                    }
                    catch (OperationCanceledException) { cancelled = true; }
                    catch (Exception ex) { failure = ex; }
                    finally { dialog.Finish(); }
                };
                dialog.Shown += shown;
                dialog.ShowDialog(owner);
            }
            if (cancelled)
            {
                onCancelled();
                return;
            }
            if (failure != null)
            {
                onFailure(failure);
                return;
            }
            completed(result);
        }

        private sealed class ProgressWindow : Form
        {
            private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
            private readonly ProgressBar _bar;
            private readonly Label _status;
            private readonly AntdUI.Button _cancel;
            private bool _finished;

            public ProgressWindow(string title, bool canCancel)
            {
                UiTheme.ConfigureForm(this);
                Text = title;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MaximizeBox = false;
                MinimizeBox = false;
                ControlBox = false;
                ClientSize = new Size(440, 160);
                _status = new Label { Text = "正在处理，请稍候。窗口仍可响应，请不要拔出磁盘。", Left = 20, Top = 20, Width = 400, Height = 44 };
                _bar = new ProgressBar { Left = 20, Top = 72, Width = 400, Height = 20, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30 };
                _cancel = UiTheme.CreateAntdButton(canCancel ? "取消操作" : "安全处理中，请勿关机", false);
                _cancel.Left = 20; _cancel.Top = 108; _cancel.Width = 400; _cancel.Height = 36;
                _cancel.Enabled = canCancel;
                _cancel.Click += delegate { RequestCancel(); };
                Controls.Add(_status); Controls.Add(_bar); Controls.Add(_cancel);
                FormClosing += delegate(object sender, FormClosingEventArgs e)
                {
                    if (_finished) return;
                    e.Cancel = true;
                    if (canCancel) RequestCancel();
                };
            }

            public IProgress<int> Progress { get; private set; }
            public CancellationToken Token { get { return _cancellation.Token; } }
            public void InitializeProgress() { Progress = new Progress<int>(UpdateProgress); }
            public void Finish()
            {
                // User-initiated closes must be blocked while work is running;
                // the worker's own completion must be allowed to close the modal.
                _finished = true;
                Close();
            }
            private void RequestCancel()
            {
                _cancellation.Cancel(); _cancel.Enabled = false;
                _status.Text = "正在安全取消，请等待当前步骤结束……";
            }
            private void UpdateProgress(int percent)
            {
                // Progress<T> posts callbacks asynchronously; they may arrive
                // after cancellation or after the dialog has already closed.
                if (_finished || IsDisposed || Disposing || _cancellation.IsCancellationRequested) return;
                if (percent < 0) { _bar.Style = ProgressBarStyle.Marquee; return; }
                _bar.Style = ProgressBarStyle.Blocks;
                _bar.Value = Math.Max(0, Math.Min(100, percent));
                _status.Text = "正在处理：" + percent + "%";
            }
            protected override void OnFormClosed(FormClosedEventArgs e)
            {
                _finished = true; _cancellation.Dispose(); base.OnFormClosed(e);
            }
        }
    }
}
