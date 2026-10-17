using FluentAssertions;
using OmenCore.Services;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Input;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class HotkeyServiceTests
    {
        [Fact]
        public void FailedHotkey_IsRetriedThenReportedOnce_WhenAnotherAppHoldsIt()
        {
            using var logging = new LoggingService();
            var attempts = 0;
            var service = new HotkeyService(logging, (_, _, _, _) => { attempts++; return false; }, (_, _) => true);
            service.Initialize(new System.IntPtr(1));

            service.RegisterHotkey(HotkeyAction.ToggleWindow, ModifierKeys.Control | ModifierKeys.Shift, Key.O).Should().BeFalse();
            for (var i = 0; i < HotkeyService.HotkeyRetryMaxTries; i++) service.RetryPendingHotkeysOnce();

            var snapshot = service.GetDiagnosticSnapshot();
            snapshot.PendingCount.Should().Be(0);
            snapshot.FailedCount.Should().Be(1);
            snapshot.FailedBindings[0].Action.Should().Be(HotkeyAction.ToggleWindow);
            attempts.Should().Be(1 + HotkeyService.HotkeyRetryMaxTries);

            service.RetryPendingHotkeysOnce();
            attempts.Should().Be(1 + HotkeyService.HotkeyRetryMaxTries, "it gives up instead of retrying forever");
        }

        [Fact]
        public void FailedHotkey_Registers_WhenTheOtherAppLetsGo()
        {
            using var logging = new LoggingService();
            var attempts = 0;
            var service = new HotkeyService(logging, (_, _, _, _) => ++attempts > 2, (_, _) => true);
            service.Initialize(new System.IntPtr(1));

            service.RegisterHotkey(HotkeyAction.ToggleWindow, ModifierKeys.Control | ModifierKeys.Shift, Key.O);
            service.RetryPendingHotkeysOnce(); // second failure
            service.RetryPendingHotkeysOnce(); // succeeds

            var snapshot = service.GetDiagnosticSnapshot();
            snapshot.RegisteredCount.Should().Be(1);
            snapshot.PendingCount.Should().Be(0);
            snapshot.FailedCount.Should().Be(0);
        }

        [Fact]
        public void Retry_DoesNothingAfterDispose()
        {
            using var logging = new LoggingService();
            var attempts = 0;
            var service = new HotkeyService(logging, (_, _, _, _) => { attempts++; return false; }, (_, _) => true);
            service.Initialize(new System.IntPtr(1));
            service.RegisterHotkey(HotkeyAction.ToggleWindow, ModifierKeys.Control | ModifierKeys.Shift, Key.O);

            service.Dispose();
            service.RetryPendingHotkeysOnce();

            attempts.Should().Be(1);
        }

        [Fact]
        public void RegisterHotkey_DoesNotQueueDuplicateAction_WhenWindowHandleIsNotReady()
        {
            using var logging = new LoggingService();
            var service = new HotkeyService(logging);

            var first = service.RegisterHotkey(HotkeyAction.ToggleWindow, ModifierKeys.Control | ModifierKeys.Shift, Key.O);
            var second = service.RegisterHotkey(HotkeyAction.ToggleWindow, ModifierKeys.Control | ModifierKeys.Shift, Key.O);

            first.Should().BeFalse();
            second.Should().BeTrue("duplicate action should be treated as already queued");
            GetPendingCount(service).Should().Be(1);
        }

        [Fact]
        public void RegisterHotkey_RejectsConflictingChord_ForDifferentAction_WhenWindowHandleIsNotReady()
        {
            using var logging = new LoggingService();
            var service = new HotkeyService(logging);

            var first = service.RegisterHotkey(HotkeyAction.ToggleWindow, ModifierKeys.Control | ModifierKeys.Shift, Key.O);
            var second = service.RegisterHotkey(HotkeyAction.ToggleFanMode, ModifierKeys.Control | ModifierKeys.Shift, Key.O);

            first.Should().BeFalse();
            second.Should().BeFalse("same key chord cannot map to two actions");
            GetPendingCount(service).Should().Be(1);
        }

        [Fact]
        public void UpdateHotkey_ReplacesPendingBinding_WhenWindowHandleIsNotReady()
        {
            using var logging = new LoggingService();
            var service = new HotkeyService(logging);

            service.RegisterHotkey(HotkeyAction.ToggleWindow, ModifierKeys.Control | ModifierKeys.Shift, Key.O)
                .Should().BeFalse();

            service.UpdateHotkey(HotkeyAction.ToggleWindow, ModifierKeys.Control | ModifierKeys.Shift, Key.P)
                .Should().BeFalse("updated binding should still be pending until a window handle is available");

            var pending = GetPending(service);
            pending.Should().ContainSingle();
            pending[0].Action.Should().Be(HotkeyAction.ToggleWindow);
            pending[0].Key.Should().Be(Key.P, "update must replace the stale pending key chord");
        }

        [Fact]
        public void RegisterDefaultHotkeys_QueuesWinF12ShowWindowFallback_WhenWindowHandleIsNotReady()
        {
            using var logging = new LoggingService();
            var service = new HotkeyService(logging);

            service.RegisterDefaultHotkeys();

            var pending = GetPending(service);
            pending.Should().Contain(binding =>
                binding.Action == HotkeyAction.ShowWindow &&
                binding.Modifiers == ModifierKeys.Windows &&
                binding.Key == Key.F12,
                "Win+F12 should open OmenCore even when firmware OMEN-key interception is unavailable");
        }

        [Fact]
        public void RegisterDefaultHotkeys_QueuesPerformanceProfileCycleHotkey()
        {
            using var logging = new LoggingService();
            var service = new HotkeyService(logging);

            service.RegisterDefaultHotkeys();

            var pending = GetPending(service);
            pending.Should().Contain(binding =>
                binding.Action == HotkeyAction.TogglePerformanceProfile &&
                binding.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) &&
                binding.Key == Key.E,
                "Ctrl+Shift+E should remain reserved for General profile cycling");
        }

        [Fact]
        public void GetDiagnosticSnapshot_ReportsPendingBindings_WhenWindowHandleIsNotReady()
        {
            using var logging = new LoggingService();
            var service = new HotkeyService(logging);

            service.RegisterDefaultHotkeys();

            var snapshot = service.GetDiagnosticSnapshot();

            snapshot.Enabled.Should().BeTrue();
            snapshot.WindowHandleReady.Should().BeFalse();
            snapshot.RegisteredCount.Should().Be(0);
            snapshot.PendingCount.Should().BeGreaterThan(0);
            snapshot.PendingBindings.Should().Contain(binding =>
                binding.Action == HotkeyAction.ShowWindow &&
                binding.Chord == "Win + F12");
        }

        [Fact]
        public void UnregisterAllHotkeys_ClearsPendingQueue()
        {
            using var logging = new LoggingService();
            var service = new HotkeyService(logging);

            service.RegisterHotkey(HotkeyAction.ToggleWindow, ModifierKeys.Control | ModifierKeys.Shift, Key.O)
                .Should().BeFalse();
            GetPendingCount(service).Should().Be(1);

            service.UnregisterAllHotkeys();

            GetPendingCount(service).Should().Be(0, "unregister all should reset both registered and queued hotkeys");
        }

        private static int GetPendingCount(HotkeyService service)
        {
            var field = typeof(HotkeyService).GetField("_pendingHotkeys", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            var pending = field!.GetValue(service) as List<HotkeyBinding>;
            pending.Should().NotBeNull();
            return pending!.Count;
        }

        private static List<HotkeyBinding> GetPending(HotkeyService service)
        {
            var field = typeof(HotkeyService).GetField("_pendingHotkeys", BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull();
            var pending = field!.GetValue(service) as List<HotkeyBinding>;
            pending.Should().NotBeNull();
            return pending!;
        }
    }
}
