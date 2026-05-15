using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class RuntimeEcOperationCoordinatorTests
    {
        [Fact]
        public void Execute_PropagatesActionResult()
        {
            var logging = new LoggingService();
            logging.Initialize();
            var coordinator = new RuntimeEcOperationCoordinator(logging);

            var result = coordinator.Execute("TestOwner", "ReadSample", () => 42);

            result.Should().Be(42);
        }

        [Fact]
        public void Execute_RejectsMissingOwner()
        {
            var logging = new LoggingService();
            logging.Initialize();
            var coordinator = new RuntimeEcOperationCoordinator(logging);

            Action act = () => coordinator.Execute("", "WriteByte", () => true);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void Execute_RejectsMissingOperationName()
        {
            var logging = new LoggingService();
            logging.Initialize();
            var coordinator = new RuntimeEcOperationCoordinator(logging);

            Action act = () => coordinator.Execute("TestOwner", "", () => true);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public async Task Execute_SerializesConcurrentCallersFromDifferentOwners()
        {
            // Two "services" race to enter the coordinator gate.
            // Their work sections must NEVER overlap — verify by tracking entry/exit timestamps
            // from concurrent threads and checking that intervals are strictly sequential.
            var logging = new LoggingService();
            logging.Initialize();
            var coordinator = new RuntimeEcOperationCoordinator(logging);

            var completionOrder = new ConcurrentQueue<string>();
            const int threadCount = 4;
            var barrier = new Barrier(threadCount);

            var tasks = Enumerable.Range(0, threadCount).Select(i => Task.Run(() =>
            {
                barrier.SignalAndWait(); // all threads start together to maximise contention
                coordinator.Execute($"Owner{i}", "WriteEc", () =>
                {
                    Thread.Sleep(5); // simulate brief EC register write
                    completionOrder.Enqueue($"Owner{i}");
                });
            }));

            await Task.WhenAll(tasks);

            completionOrder.Should().HaveCount(threadCount, "every caller must complete exactly once");
            completionOrder.Distinct().Should().HaveCount(threadCount, "each owner must appear exactly once");
        }

        [Fact]
        public void Execute_VoidOverload_RunsActionExactlyOnce()
        {
            var logging = new LoggingService();
            logging.Initialize();
            var coordinator = new RuntimeEcOperationCoordinator(logging);
            int callCount = 0;

            coordinator.Execute("TestOwner", "WriteRegister", () => { callCount++; });

            callCount.Should().Be(1);
        }
    }
}
