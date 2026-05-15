using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using OmenCore.Hardware;
using OmenCore.Services;
using OmenCore.Services.KeyboardLighting;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class EcDirectBackendCoordinatorTests
    {
        private sealed class FakeEcAccess : IEcAccess
        {
            private readonly Dictionary<ushort, byte> _registers = new();

            public bool IsAvailable => true;

            public bool Initialize(string devicePath) => true;

            public byte ReadByte(ushort address)
            {
                return _registers.TryGetValue(address, out var value) ? value : (byte)0;
            }

            public void WriteByte(ushort address, byte value)
            {
                _registers[address] = value;
            }

            public int ReadWord(ushort lowAddress, ushort highAddress)
            {
                return ReadByte(lowAddress) | (ReadByte(highAddress) << 8);
            }

            public void Dispose()
            {
            }
        }

        [Fact]
        public async Task SetBrightnessAsync_WaitsForSharedEcCoordinator()
        {
            var logging = new LoggingService();
            var coordinator = new RuntimeEcOperationCoordinator(logging);
            var backend = new EcDirectBackend(new FakeEcAccess(), logging, ecOperationCoordinator: coordinator);
            (await backend.InitializeAsync()).Should().BeTrue();

            using var acquired = new ManualResetEventSlim(false);
            Exception? holderError = null;
            var holder = new Thread(() =>
            {
                try
                {
                    coordinator.Execute("Test", "HoldEcGate", () =>
                    {
                        acquired.Set();
                        Thread.Sleep(175);
                    });
                }
                catch (Exception ex)
                {
                    holderError = ex;
                }
            });
            holder.Start();

            acquired.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();

            var sw = Stopwatch.StartNew();
            var applied = await backend.SetBrightnessAsync(80);
            sw.Stop();

            applied.Should().BeTrue();
            sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(125);
            holder.Join(TimeSpan.FromSeconds(2)).Should().BeTrue();
            holderError.Should().BeNull();
        }
    }
}
