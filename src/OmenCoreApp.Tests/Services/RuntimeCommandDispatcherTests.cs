using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class RuntimeCommandDispatcherTests
    {
        [Fact]
        public async Task EnqueueLatest_WhenIdle_ExecutesCommand()
        {
            using var dispatcher = new RuntimeCommandDispatcher("Test");
            var executed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            dispatcher.EnqueueLatest("one", () =>
            {
                executed.TrySetResult(true);
                return Task.CompletedTask;
            });

            var finished = await Task.WhenAny(executed.Task, Task.Delay(2000));
            finished.Should().Be(executed.Task, "queued command should execute");
            (await executed.Task).Should().BeTrue();
        }

        [Fact]
        public async Task EnqueueLatest_WhileRunning_DropsIntermediateCommands()
        {
            using var dispatcher = new RuntimeCommandDispatcher("Test");
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finalRan = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var secondExecuted = 0;
            var thirdExecuted = 0;

            dispatcher.EnqueueLatest("first", async () =>
            {
                firstStarted.TrySetResult(true);
                await releaseFirst.Task;
            });

            var started = await Task.WhenAny(firstStarted.Task, Task.Delay(2000));
            started.Should().Be(firstStarted.Task, "first command should start promptly");

            dispatcher.EnqueueLatest("second", () =>
            {
                Interlocked.Increment(ref secondExecuted);
                return Task.CompletedTask;
            });

            dispatcher.EnqueueLatest("third", () =>
            {
                Interlocked.Increment(ref thirdExecuted);
                finalRan.TrySetResult(true);
                return Task.CompletedTask;
            });

            releaseFirst.TrySetResult(true);

            var finished = await Task.WhenAny(finalRan.Task, Task.Delay(3000));
            finished.Should().Be(finalRan.Task, "latest pending command should run after first command exits");
            secondExecuted.Should().Be(0, "intermediate command should be replaced by last-write-wins queueing");
            thirdExecuted.Should().Be(1);
        }

        [Fact]
        public async Task Dispose_StopsWorker_BeforeRunningPendingCommand()
        {
            var dispatcher = new RuntimeCommandDispatcher("Test");
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pendingRan = 0;

            dispatcher.EnqueueLatest("first", async () =>
            {
                firstStarted.TrySetResult(true);
                await releaseFirst.Task;
            });

            var started = await Task.WhenAny(firstStarted.Task, Task.Delay(2000));
            started.Should().Be(firstStarted.Task);

            dispatcher.EnqueueLatest("pending", () =>
            {
                Interlocked.Increment(ref pendingRan);
                return Task.CompletedTask;
            });

            dispatcher.Dispose();
            releaseFirst.TrySetResult(true);

            await Task.Delay(200);
            pendingRan.Should().Be(0, "pending command should not execute after dispatcher disposal");
        }

        [Fact]
        public async Task EnqueueLatest_WithConcurrentProducers_PreservesLatestWinsBehavior()
        {
            using var dispatcher = new RuntimeCommandDispatcher("Test");
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finalRan = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            dispatcher.EnqueueLatest("first", async () =>
            {
                firstStarted.TrySetResult(true);
                await releaseFirst.Task;
            });

            var started = await Task.WhenAny(firstStarted.Task, Task.Delay(2000));
            started.Should().Be(firstStarted.Task, "first command should start before concurrent enqueue burst");

            var names = new[] { "tray", "hotkey", "automation", "osd" };
            var producers = names.Select(name => Task.Run(() =>
            {
                dispatcher.EnqueueLatest(name, () =>
                {
                    finalRan.TrySetResult(name);
                    return Task.CompletedTask;
                });
            }));

            await Task.WhenAll(producers);
            dispatcher.EnqueueLatest("final", () =>
            {
                finalRan.TrySetResult("final");
                return Task.CompletedTask;
            });
            releaseFirst.TrySetResult(true);

            var finished = await Task.WhenAny(finalRan.Task, Task.Delay(3000));
            finished.Should().Be(finalRan.Task, "one latest command should run after first command completes");
            (await finalRan.Task).Should().Be("final", "last producer must win under concurrent enqueue pressure");
        }

        [Fact]
        public async Task EnqueueLatest_UnderSustainedCongestion_ExecutesOnlyFirstAndDeterministicFinalIntent()
        {
            using var dispatcher = new RuntimeCommandDispatcher("Test");
            var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var finalRan = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            var executedCount = 0;

            dispatcher.EnqueueLatest("first", async () =>
            {
                Interlocked.Increment(ref executedCount);
                firstStarted.TrySetResult(true);
                await releaseFirst.Task;
            });

            var started = await Task.WhenAny(firstStarted.Task, Task.Delay(2000));
            started.Should().Be(firstStarted.Task, "first command should start before pressure burst");

            const int burstCount = 500;
            for (var i = 0; i < burstCount; i++)
            {
                var intentId = i;
                dispatcher.EnqueueLatest($"intent-{intentId}", () =>
                {
                    Interlocked.Increment(ref executedCount);
                    finalRan.TrySetResult(intentId);
                    return Task.CompletedTask;
                });
            }

            dispatcher.EnqueueLatest("intent-final", () =>
            {
                Interlocked.Increment(ref executedCount);
                finalRan.TrySetResult(burstCount);
                return Task.CompletedTask;
            });

            releaseFirst.TrySetResult(true);

            var finished = await Task.WhenAny(finalRan.Task, Task.Delay(5000));
            finished.Should().Be(finalRan.Task, "final intent should execute after worker is released");
            (await finalRan.Task).Should().Be(burstCount, "explicit deterministic final enqueue should win after congestion burst");
            executedCount.Should().Be(2, "latest-wins queue should execute only the running command and the final pending command");
        }

        [Fact]
        public async Task EnqueueLatest_UnderRepeatedBursts_RemainsDeterministicWithoutDrift()
        {
            using var dispatcher = new RuntimeCommandDispatcher("Test");
            var totalExecuted = 0;
            const int rounds = 20;

            for (var round = 0; round < rounds; round++)
            {
                var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                var finalRan = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

                dispatcher.EnqueueLatest($"round-{round}-first", async () =>
                {
                    Interlocked.Increment(ref totalExecuted);
                    firstStarted.TrySetResult(true);
                    await releaseFirst.Task;
                });

                var started = await Task.WhenAny(firstStarted.Task, Task.Delay(2000));
                started.Should().Be(firstStarted.Task, $"round {round}: first command should start before burst enqueue");

                for (var i = 0; i < 25; i++)
                {
                    var intentId = i;
                    dispatcher.EnqueueLatest($"round-{round}-intent-{intentId}", () =>
                    {
                        Interlocked.Increment(ref totalExecuted);
                        finalRan.TrySetResult(intentId);
                        return Task.CompletedTask;
                    });
                }

                dispatcher.EnqueueLatest($"round-{round}-final", () =>
                {
                    Interlocked.Increment(ref totalExecuted);
                    finalRan.TrySetResult(25);
                    return Task.CompletedTask;
                });

                releaseFirst.TrySetResult(true);

                var finished = await Task.WhenAny(finalRan.Task, Task.Delay(4000));
                finished.Should().Be(finalRan.Task, $"round {round}: final intent should converge");
                (await finalRan.Task).Should().Be(25, $"round {round}: explicit final enqueue should deterministically win");
            }

            totalExecuted.Should().Be(rounds * 2, "each round should execute exactly the first command and one final winning command");
        }
    }
}
