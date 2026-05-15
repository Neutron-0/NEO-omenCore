using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class RuntimeHotkeyCoordinatorTests
    {
        [Fact]
        public async Task EnqueueUiAction_WithoutDispatcher_ExecutesAction()
        {
            using var coordinator = new RuntimeHotkeyCoordinator(() => null);
            var executed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            coordinator.EnqueueUiAction("one", () => executed.TrySetResult(true));

            var finished = await Task.WhenAny(executed.Task, Task.Delay(2000));
            finished.Should().Be(executed.Task);
            (await executed.Task).Should().BeTrue();
        }

        [Fact]
        public async Task EnqueueUiAction_UsesLatestWins_WhenActionsOverlap()
        {
            using var coordinator = new RuntimeHotkeyCoordinator(() => null);
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finalRan = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondCount = 0;
            var thirdCount = 0;

            coordinator.EnqueueUiAction("first", () =>
            {
                firstStarted.TrySetResult(true);
                releaseFirst.Task.GetAwaiter().GetResult();
            });

            // Full-suite execution can briefly delay thread-pool worker startup; allow a wider window.
            var started = await Task.WhenAny(firstStarted.Task, Task.Delay(10000));
            started.Should().Be(firstStarted.Task);

            coordinator.EnqueueUiAction("second", () => Interlocked.Increment(ref secondCount));
            coordinator.EnqueueUiAction("third", () =>
            {
                Interlocked.Increment(ref thirdCount);
                finalRan.TrySetResult(true);
            });

            releaseFirst.TrySetResult(true);

            var finished = await Task.WhenAny(finalRan.Task, Task.Delay(10000));
            finished.Should().Be(finalRan.Task);
            secondCount.Should().Be(0);
            thirdCount.Should().Be(1);
        }
    }
}
