using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace OmenCore.Services
{
    /// <summary>
    /// Centralizes hotkey action dispatch so MainViewModel does not own hotkey queue
    /// orchestration and UI-dispatch scheduling internals.
    /// </summary>
    public sealed class RuntimeHotkeyCoordinator : IDisposable
    {
        private readonly Func<Dispatcher?> _dispatcherProvider;
        private readonly RuntimeCommandDispatcher _dispatcher;
        private readonly Action<string>? _onBeginInvokeScheduled;

        public RuntimeHotkeyCoordinator(
            Func<Dispatcher?> dispatcherProvider,
            Action<string>? logDebug = null,
            Action<string>? logWarn = null,
            Action<string>? logInfo = null,
            Action<string>? onBeginInvokeScheduled = null)
        {
            _dispatcherProvider = dispatcherProvider ?? throw new ArgumentNullException(nameof(dispatcherProvider));
            _dispatcher = new RuntimeCommandDispatcher(
                contextName: "HotkeyAction",
                logDebug: logDebug,
                logWarn: logWarn,
                logInfo: logInfo);
            _onBeginInvokeScheduled = onBeginInvokeScheduled;
        }

        public void EnqueueUiAction(string actionName, Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            _dispatcher.EnqueueLatest(actionName, () =>
            {
                var uiDispatcher = _dispatcherProvider();
                if (uiDispatcher == null)
                {
                    action();
                    return Task.CompletedTask;
                }

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _onBeginInvokeScheduled?.Invoke($"Hotkey:{actionName}");
                uiDispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        action();
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }));

                return tcs.Task;
            });
        }

        public void Dispose()
        {
            _dispatcher.Dispose();
        }
    }
}
