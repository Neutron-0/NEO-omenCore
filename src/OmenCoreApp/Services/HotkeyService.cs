using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using OmenCore.Models;
using OmenCore.Utils;

namespace OmenCore.Services
{
    /// <summary>
    /// Service for managing global keyboard hotkeys
    /// </summary>
    public class HotkeyService : IDisposable
    {
        #region Win32 API

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        #endregion

        private const int WM_HOTKEY = 0x0312;

        // Modifier keys
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        private readonly LoggingService _logging;
        private readonly Dictionary<int, HotkeyBinding> _registeredHotkeys = new();
        private readonly List<HotkeyBinding> _pendingHotkeys = new(); // Hotkeys queued for retry
        private IntPtr _windowHandle;
        private HwndSource? _source;
        private int _nextHotkeyId = 1;
        private bool _isEnabled = true;
        private System.Threading.Timer? _retryTimer;
        private int _retryAttempts = 0;
        private const int MaxRetryAttempts = 5;
        private const int RetryIntervalMs = 2000;

        // A key another app holds is retried for a bounded time (12 tries, 5 s apart), then reported once.
        internal const int HotkeyRetryIntervalMs = 5000;
        internal const int HotkeyRetryMaxTries = 12;
        private System.Threading.Timer? _hotkeyRetryTimer;
        private int _hotkeyRetryTries;
        private readonly List<HotkeyBinding> _failedHotkeys = new(); // gave up: held by another app
        private readonly Func<IntPtr, int, uint, uint, bool> _registerHotKey;
        private readonly Func<IntPtr, int, bool> _unregisterHotKey;

        // Events for hotkey actions
        public event EventHandler? ToggleFanModeRequested;
        public event EventHandler? ToggleMaxFanRequested;
        public event EventHandler? TogglePerformanceModeRequested;
        public event EventHandler? TogglePerformanceProfileRequested;
        public event EventHandler? ToggleBoostModeRequested;
        public event EventHandler? ToggleQuietModeRequested;
        public event EventHandler? ShowWindowRequested;
        public event EventHandler? HideWindowRequested;
        public event EventHandler? ToggleWindowRequested;
        public event EventHandler? OpenFanControlRequested;
        public event EventHandler? OpenDashboardRequested;
        public event EventHandler? TakeScreenshotRequested;
        public event EventHandler? ApplySettingsRequested;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                _logging.Info($"Hotkeys {(_isEnabled ? "enabled" : "disabled")}");
            }
        }

        public IReadOnlyDictionary<int, HotkeyBinding> RegisteredHotkeys => _registeredHotkeys;

        public HotkeyService(LoggingService logging) : this(logging, RegisterHotKey, UnregisterHotKey)
        {
        }

        /// <summary>The Win32 calls are injectable so tests can make a key fail to register.</summary>
        internal HotkeyService(LoggingService logging,
            Func<IntPtr, int, uint, uint, bool> registerHotKey, Func<IntPtr, int, bool> unregisterHotKey)
        {
            _logging = logging;
            _registerHotKey = registerHotKey;
            _unregisterHotKey = unregisterHotKey;
        }

        public HotkeyDiagnosticSnapshot GetDiagnosticSnapshot()
        {
            return new HotkeyDiagnosticSnapshot
            {
                Enabled = _isEnabled,
                WindowHandleReady = _windowHandle != IntPtr.Zero,
                RegisteredCount = _registeredHotkeys.Count,
                PendingCount = _pendingHotkeys.Count,
                FailedCount = _failedHotkeys.Count,
                FailedBindings = _failedHotkeys
                    .OrderBy(binding => binding.Action)
                    .Select(binding => HotkeyDiagnosticBinding.From(binding))
                    .ToArray(),
                RegisteredBindings = _registeredHotkeys.Values
                    .OrderBy(binding => binding.Action)
                    .Select(binding => HotkeyDiagnosticBinding.From(binding))
                    .ToArray(),
                PendingBindings = _pendingHotkeys
                    .OrderBy(binding => binding.Action)
                    .Select(binding => HotkeyDiagnosticBinding.From(binding))
                    .ToArray()
            };
        }

        /// <summary>
        /// Initialize hotkey handling with the main window
        /// </summary>
        public void Initialize(IntPtr windowHandle)
        {
            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source.Dispose();
                _source = null;
            }

            _windowHandle = windowHandle;
            _source = HwndSource.FromHwnd(windowHandle);
            _source?.AddHook(WndProc);
            
            _logging.Info("HotkeyService initialized");
            
            // Retry any pending hotkeys that failed earlier
            RetryPendingHotkeys();
        }
        
        /// <summary>
        /// Initialize with retry logic for cases where window handle isn't ready immediately.
        /// This is useful when starting minimized to tray.
        /// </summary>
        public void InitializeWithRetry(Func<IntPtr> getWindowHandle)
        {
            _retryTimer?.Dispose();
            _retryTimer = null;

            var handle = getWindowHandle();
            if (handle != IntPtr.Zero)
            {
                Initialize(handle);
                return;
            }
            
            // Window not ready yet, start retry timer
            _logging.Info("HotkeyService: Window handle not ready, starting retry timer...");
            _retryAttempts = 0;
            _retryTimer = new System.Threading.Timer(_ =>
            {
                try
                {
                    DispatcherHelper.RunOnUiThread(() =>
                    {
                        var h = getWindowHandle();
                        if (h != IntPtr.Zero)
                        {
                            Initialize(h);
                            _retryTimer?.Dispose();
                            _retryTimer = null;
                            _logging.Info($"HotkeyService: Initialized after {_retryAttempts + 1} attempts");
                        }
                        else
                        {
                            _retryAttempts++;
                            if (_retryAttempts >= MaxRetryAttempts)
                            {
                                _logging.Warn($"HotkeyService: Failed to initialize after {MaxRetryAttempts} attempts");
                                _retryTimer?.Dispose();
                                _retryTimer = null;
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logging.Error($"HotkeyService retry error: {ex.Message}", ex);
                }
            }, null, RetryIntervalMs, RetryIntervalMs);
        }
        
        /// <summary>
        /// Retry registering hotkeys that failed initially
        /// </summary>
        private void RetryPendingHotkeys()
        {
            if (_pendingHotkeys.Count == 0) return;
            
            _logging.Info($"HotkeyService: Retrying {_pendingHotkeys.Count} pending hotkey(s)...");
            var toRetry = _pendingHotkeys.ToList();
            _pendingHotkeys.Clear();
            
            foreach (var binding in toRetry)
            {
                RegisterHotkey(binding.Action, binding.Modifiers, binding.Key);
            }
        }

        private DateTime _lastFailedRetryUtc = DateTime.MinValue;
        internal static readonly TimeSpan FailedRetryMinGap = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Gives keys that were abandoned (held by another app) another bounded round of tries. Meant for window activation: the
        /// other app may have been closed since. Rate limited so a key that stays blocked does not restart the timer on every focus.
        /// </summary>
        public void RetryFailedHotkeys(DateTime? nowUtc = null)
        {
            var now = nowUtc ?? DateTime.UtcNow;
            if (_failedHotkeys.Count == 0 || now - _lastFailedRetryUtc < FailedRetryMinGap) return;

            _lastFailedRetryUtc = now;
            _logging.Info($"Hotkey retry on activation: trying {_failedHotkeys.Count} previously blocked hotkey(s) again");
            foreach (var binding in _failedHotkeys.ToList())
                RegisterHotkey(binding.Action, binding.Modifiers, binding.Key);
        }

        /// <summary>Plain-text list of hotkeys another app is holding, for Settings. Empty when there are none.</summary>
        public string DescribeFailedHotkeys() =>
            _failedHotkeys.Count == 0
                ? string.Empty
                : "Held by another app, not working: " + string.Join(", ", _failedHotkeys.Select(b => $"{b.DisplayString} ({b.Action})"));

        private void StartHotkeyRetryTimer()
        {
            if (_hotkeyRetryTimer != null) return;
            _hotkeyRetryTries = 0;
            _hotkeyRetryTimer = new System.Threading.Timer(_ =>
            {
                try { DispatcherHelper.RunOnUiThread(RetryPendingHotkeysOnce); } // RegisterHotKey must run on the window's thread
                catch (Exception ex) { _logging.Error($"Hotkey retry error: {ex.Message}", ex); }
            }, null, HotkeyRetryIntervalMs, HotkeyRetryIntervalMs);
        }

        /// <summary>One bounded retry round. When the tries run out, each key still failing is reported once and kept in <see cref="HotkeyDiagnosticSnapshot.FailedBindings"/>.</summary>
        internal void RetryPendingHotkeysOnce()
        {
            if (_pendingHotkeys.Count == 0 || _windowHandle == IntPtr.Zero)
            {
                StopHotkeyRetryTimer();
                return;
            }

            _hotkeyRetryTries++;
            var toRetry = _pendingHotkeys.ToList();
            _pendingHotkeys.Clear();
            foreach (var binding in toRetry)
                RegisterHotkey(binding.Action, binding.Modifiers, binding.Key);

            if (_pendingHotkeys.Count == 0)
            {
                _logging.Info($"Hotkey retry: all pending hotkeys registered after {_hotkeyRetryTries} retr{(_hotkeyRetryTries == 1 ? "y" : "ies")}");
                StopHotkeyRetryTimer();
            }
            else if (_hotkeyRetryTries >= HotkeyRetryMaxTries)
            {
                foreach (var binding in _pendingHotkeys)
                {
                    _failedHotkeys.Add(binding);
                    _logging.Warn($"Hotkey {binding.DisplayString} for {binding.Action} could not be registered after {HotkeyRetryMaxTries} tries: it is held by another app. Choose a different key in Settings.");
                }
                _pendingHotkeys.Clear();
                StopHotkeyRetryTimer();
            }
        }

        private void StopHotkeyRetryTimer()
        {
            _hotkeyRetryTimer?.Dispose();
            _hotkeyRetryTimer = null;
        }

        /// <summary>
        /// Register default hotkeys
        /// v2.6.0: Removed Ctrl+S which conflicted with save in other applications (Issue #53)
        /// v3.0.0: optional window-focused hotkey registration handled by MainViewModel
        /// </summary>
        public void RegisterDefaultHotkeys()
        {
            // Ctrl+Shift+F = Cycle fan modes
            RegisterHotkey(HotkeyAction.ToggleFanMode, ModifierKeys.Control | ModifierKeys.Shift, Key.F);

            // Ctrl+Shift+M = Toggle max fan mode
            RegisterHotkey(HotkeyAction.ToggleMaxFan, ModifierKeys.Control | ModifierKeys.Shift, Key.M);
            
            // Ctrl+Shift+P = Cycle performance modes
            RegisterHotkey(HotkeyAction.TogglePerformanceMode, ModifierKeys.Control | ModifierKeys.Shift, Key.P);

            // Ctrl+Shift+E = Cycle General performance profiles
            RegisterHotkey(HotkeyAction.TogglePerformanceProfile, ModifierKeys.Control | ModifierKeys.Shift, Key.E);
            
            // Ctrl+Shift+B = Toggle boost mode
            RegisterHotkey(HotkeyAction.ToggleBoostMode, ModifierKeys.Control | ModifierKeys.Shift, Key.B);
            
            // Ctrl+Shift+Q = Toggle quiet mode
            RegisterHotkey(HotkeyAction.ToggleQuietMode, ModifierKeys.Control | ModifierKeys.Shift, Key.Q);
            
            // Ctrl+Shift+O = Show/hide OmenCore window
            RegisterHotkey(HotkeyAction.ToggleWindow, ModifierKeys.Control | ModifierKeys.Shift, Key.O);

            // Win+F12 = Show OmenCore window. Several HP keyboards expose the OMEN launch
            // chord near F12, and this gives users a predictable fallback when firmware
            // OMEN-key interception is not available on their model.
            RegisterHotkey(HotkeyAction.ShowWindow, ModifierKeys.Windows, Key.F12);
            
            // v2.6.0: REMOVED Ctrl+S - it conflicts with save shortcuts in Photoshop, VSCode, etc.
            // Users can still click the Apply button or use Ctrl+Shift+Alt+A if they need a hotkey
            // RegisterHotkey(HotkeyAction.ApplySettings, ModifierKeys.Control, Key.S); // DISABLED
            
            // v2.6.0: Use safe multi-modifier combo that won't conflict with other apps
            RegisterHotkey(HotkeyAction.ApplySettings, ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt, Key.A);
            
            _logging.Info($"Registered {_registeredHotkeys.Count} default hotkeys");
        }

        /// <summary>
        /// Register a hotkey binding
        /// </summary>
        public bool RegisterHotkey(HotkeyAction action, ModifierKeys modifiers, Key key)
        {
            _failedHotkeys.RemoveAll(f => f.Action == action); // a new attempt replaces an earlier give-up
            // Skip if this action is already registered (e.g. ToggleWindow registered globally
            // before window-focused hotkeys are layered on top via OnMainWindowActivated).
            foreach (var existing in _registeredHotkeys.Values)
            {
                if (existing.Action == action)
                {
                    _logging.Debug($"Hotkey {action} already registered — skipping duplicate");
                    return true;
                }
            }

            // Skip if this action is already queued for delayed registration.
            foreach (var pending in _pendingHotkeys)
            {
                if (pending.Action == action)
                {
                    _logging.Debug($"Hotkey {action} already queued — skipping duplicate");
                    return true;
                }
            }

            // Prevent conflicting registrations for the same key chord.
            if (_registeredHotkeys.Values.Any(existing => existing.Modifiers == modifiers && existing.Key == key && existing.Action != action) ||
                _pendingHotkeys.Any(existing => existing.Modifiers == modifiers && existing.Key == key && existing.Action != action))
            {
                _logging.Warn($"Failed to register hotkey: {modifiers}+{key} for {action}, chord is already bound to a different action");
                return false;
            }

            if (_windowHandle == IntPtr.Zero)
            {
                // Queue for later registration
                _pendingHotkeys.Add(new HotkeyBinding
                {
                    Action = action,
                    Modifiers = modifiers,
                    Key = key
                });
                _logging.Info($"Hotkey {modifiers}+{key} queued for registration (window not ready)");
                return false;
            }

            // Convert WPF keys to Win32
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            uint mod = ConvertModifiers(modifiers);

            int id = _nextHotkeyId++;
            
            if (_registerHotKey(_windowHandle, id, mod | MOD_NOREPEAT, vk))
            {
                var binding = new HotkeyBinding
                {
                    Id = id,
                    Action = action,
                    Modifiers = modifiers,
                    Key = key
                };
                _registeredHotkeys[id] = binding;
                _logging.Info($"Registered hotkey: {binding}");
                return true;
            }
            else
            {
                // Failed to register - queue for retry
                _pendingHotkeys.Add(new HotkeyBinding
                {
                    Action = action,
                    Modifiers = modifiers,
                    Key = key
                });
                _logging.Warn($"Failed to register hotkey: {modifiers}+{key} for {action}, queued for retry");
                StartHotkeyRetryTimer();
                return false;
            }
        }

        /// <summary>
        /// Unregister a specific hotkey
        /// </summary>
        public bool UnregisterHotkey(int id)
        {
            if (_registeredHotkeys.TryGetValue(id, out var binding))
            {
                _unregisterHotKey(_windowHandle, id);
                _registeredHotkeys.Remove(id);
                _logging.Info($"Unregistered hotkey: {binding}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Unregister all hotkeys
        /// </summary>
        public void UnregisterAllHotkeys()
        {
            foreach (var id in _registeredHotkeys.Keys)
            {
                _unregisterHotKey(_windowHandle, id);
            }
            _registeredHotkeys.Clear();
            _pendingHotkeys.Clear();
            _logging.Info("Unregistered all hotkeys");
        }

        /// <summary>
        /// Unregister all hotkeys except the specified action.
        /// Used by window-focus mode to preserve always-on hotkeys like ToggleWindow
        /// even when the window loses focus or is hidden to tray.
        /// </summary>
        public void UnregisterAllExcept(params HotkeyAction[] preserveActions)
        {
            var preserve = preserveActions.Length == 0
                ? new HashSet<HotkeyAction>()
                : new HashSet<HotkeyAction>(preserveActions);
            var toRemove = new List<int>();
            foreach (var kvp in _registeredHotkeys)
            {
                if (!preserve.Contains(kvp.Value.Action))
                {
                    _unregisterHotKey(_windowHandle, kvp.Key);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var id in toRemove)
                _registeredHotkeys.Remove(id);
            _logging.Info($"Unregistered all hotkeys except {string.Join(", ", preserve)}");
        }

        /// <summary>
        /// Update a hotkey binding
        /// </summary>
        public bool UpdateHotkey(HotkeyAction action, ModifierKeys modifiers, Key key)
        {
            // Find and remove existing binding for this action
            int? existingId = null;
            foreach (var kvp in _registeredHotkeys)
            {
                if (kvp.Value.Action == action)
                {
                    existingId = kvp.Key;
                    break;
                }
            }

            if (existingId.HasValue)
            {
                UnregisterHotkey(existingId.Value);
            }

            // If the action was queued before window initialization, replace that pending
            // entry so update requests don't silently keep stale key chords.
            _pendingHotkeys.RemoveAll(p => p.Action == action);

            // Register new binding
            return RegisterHotkey(action, modifiers, key);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && _isEnabled)
            {
                // Check if we should suppress hotkeys during RDP sessions
                if (ShouldSuppressForRdp())
                {
                    _logging.Debug("Hotkey suppressed - RDP session active");
                    return IntPtr.Zero;
                }
                
                int id = wParam.ToInt32();
                if (_registeredHotkeys.TryGetValue(id, out var binding))
                {
                    handled = true;
                    HandleHotkeyPressed(binding);
                }
            }
            return IntPtr.Zero;
        }

        private void HandleHotkeyPressed(HotkeyBinding binding)
        {
            _logging.Info($"Hotkey pressed: {binding}");

            switch (binding.Action)
            {
                case HotkeyAction.ToggleFanMode:
                    ToggleFanModeRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.ToggleMaxFan:
                    ToggleMaxFanRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.TogglePerformanceMode:
                    TogglePerformanceModeRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.TogglePerformanceProfile:
                    TogglePerformanceProfileRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.ToggleBoostMode:
                    ToggleBoostModeRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.ToggleQuietMode:
                    ToggleQuietModeRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.ShowWindow:
                    ShowWindowRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.HideWindow:
                    HideWindowRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.ToggleWindow:
                    ToggleWindowRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.OpenFanControl:
                    OpenFanControlRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.OpenDashboard:
                    OpenDashboardRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.TakeScreenshot:
                    TakeScreenshotRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case HotkeyAction.ApplySettings:
                    ApplySettingsRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        private uint ConvertModifiers(ModifierKeys modifiers)
        {
            uint result = MOD_NONE;
            if (modifiers.HasFlag(ModifierKeys.Alt)) result |= MOD_ALT;
            if (modifiers.HasFlag(ModifierKeys.Control)) result |= MOD_CONTROL;
            if (modifiers.HasFlag(ModifierKeys.Shift)) result |= MOD_SHIFT;
            if (modifiers.HasFlag(ModifierKeys.Windows)) result |= MOD_WIN;
            return result;
        }
        
        /// <summary>
        /// Check if hotkeys should be suppressed due to RDP session.
        /// Uses App.ShouldSuppressWindowActivation which tracks remote/locked session state.
        /// </summary>
        private bool ShouldSuppressForRdp()
        {
            try
            {
                // Check configuration setting first
                var config = App.Configuration?.Config;
                if (config?.Features?.SuppressHotkeysInRdp != true)
                {
                    return false; // User disabled RDP suppression
                }
                
                // Check if we're in a remote session
                return App.ShouldSuppressWindowActivation;
            }
            catch
            {
                return false; // Fail-safe: don't suppress if we can't check
            }
        }

        public void Dispose()
        {
            StopHotkeyRetryTimer();
            _retryTimer?.Dispose();
            _retryTimer = null;
            _pendingHotkeys.Clear();
            UnregisterAllHotkeys();
            _source?.RemoveHook(WndProc);
            _source?.Dispose();
        }
    }

    /// <summary>
    /// Available hotkey actions
    /// </summary>
    public enum HotkeyAction
    {
        ToggleFanMode,
        ToggleMaxFan,
        TogglePerformanceMode,
        TogglePerformanceProfile,
        ToggleBoostMode,
        ToggleQuietMode,
        ShowWindow,
        HideWindow,
        ToggleWindow,
        OpenFanControl,
        OpenDashboard,
        TakeScreenshot,
        ApplySettings
    }

    /// <summary>
    /// Represents a hotkey binding
    /// </summary>
    public class HotkeyBinding
    {
        public int Id { get; set; }
        public HotkeyAction Action { get; set; }
        public ModifierKeys Modifiers { get; set; }
        public Key Key { get; set; }
        public bool IsEnabled { get; set; } = true;

        public override string ToString()
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(Key.ToString());
            return $"{string.Join("+", parts)} → {Action}";
        }

        public string DisplayString
        {
            get
            {
                var parts = new List<string>();
                if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
                if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
                if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
                if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
                parts.Add(Key.ToString());
                return string.Join(" + ", parts);
            }
        }
    }

    public sealed class HotkeyDiagnosticSnapshot
    {
        public bool Enabled { get; init; }
        public bool WindowHandleReady { get; init; }
        public int RegisteredCount { get; init; }
        public int PendingCount { get; init; }
        public int FailedCount { get; init; }
        public HotkeyDiagnosticBinding[] FailedBindings { get; init; } = Array.Empty<HotkeyDiagnosticBinding>();
        public HotkeyDiagnosticBinding[] RegisteredBindings { get; init; } = Array.Empty<HotkeyDiagnosticBinding>();
        public HotkeyDiagnosticBinding[] PendingBindings { get; init; } = Array.Empty<HotkeyDiagnosticBinding>();
    }

    public sealed class HotkeyDiagnosticBinding
    {
        public int Id { get; init; }
        public HotkeyAction Action { get; init; }
        public string Chord { get; init; } = string.Empty;
        public bool IsEnabled { get; init; }

        public static HotkeyDiagnosticBinding From(HotkeyBinding binding)
        {
            return new HotkeyDiagnosticBinding
            {
                Id = binding.Id,
                Action = binding.Action,
                Chord = binding.DisplayString,
                IsEnabled = binding.IsEnabled
            };
        }
    }
}
