using System;
using System.IO;
using FluentAssertions;
using NvpwrControlBlackwell;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    /// <summary>
    /// A synthetic ROM (no user's ROM is used) holding one board-power record of 5 / 80 / 115 W with a base of 100 W, the shape the
    /// Prophecy author reported for the HP OMEN RTX 5070 Ti board. Before the variant the resolver could not match it to a 140 W
    /// profile; with the variant it resolves, and only for that board.
    /// </summary>
    public class NvidiaOmen115RomTests : IDisposable
    {
        private const string Omen = @"PCI\VEN_10DE&DEV_2F58&SUBSYS_8E71103C";
        private const string OtherBoard = @"PCI\VEN_10DE&DEV_2F58&SUBSYS_1234103C";
        private const string Name = "NVIDIA GeForce RTX 5070 Ti Laptop GPU";

        private readonly string _dir = Path.Combine(Path.GetTempPath(), "omen115-" + Guid.NewGuid().ToString("N"));

        public NvidiaOmen115RomTests()
        {
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch (IOException) { }
            RemoveFixtureCacheLines();
        }

        // A successful resolve writes a line to the machine-wide VBIOS cache (the backend has no way to point it elsewhere).
        // The fixture's VBIOS string is "fixture", which no real card reports; remove those lines so a test run leaves nothing behind.
        private static void RemoveFixtureCacheLines()
        {
            var cache = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "OmenCoreProphecy", "prophecy-state", "vbios-resolver-cache-v3.txt");
            try
            {
                if (!File.Exists(cache)) return;
                var keep = Array.FindAll(File.ReadAllLines(cache), line => !line.Contains("|fixture|"));
                if (keep.Length == 0) File.Delete(cache); else File.WriteAllLines(cache, keep);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Cleanup only; a locked cache file is left alone.
            }
        }

        private (string Path, int Entry) WriteRom(ushort pcirDevice = 0x2F58)
        {
            var bytes = new byte[0x10000];
            bytes[0] = 0x55; bytes[1] = 0xAA; bytes[0x18] = 0x40;
            System.Text.Encoding.ASCII.GetBytes("PCIR").CopyTo(bytes, 0x40);
            BitConverter.GetBytes((ushort)0x10DE).CopyTo(bytes, 0x44);
            BitConverter.GetBytes(pcirDevice).CopyTo(bytes, 0x46);

            const int table = 0x200;
            var entry = table + 0x38 + 2 * 0x66;
            bytes[table] = 0x40; bytes[table + 1] = 0x38; bytes[table + 2] = 0x66; bytes[table + 3] = 3;
            foreach (var (offset, milliwatts) in new[] { (0x0D, 5000u), (0x11, 80000u), (0x15, 115000u), (0x49, 100000u) })
                BitConverter.GetBytes(milliwatts).CopyTo(bytes, entry + offset);

            var path = Path.Combine(_dir, "synthetic.rom");
            File.WriteAllBytes(path, bytes);
            return (path, entry);
        }

        [Fact]
        public void TheHpOmenBoard_ResolvesA115WattRom()
        {
            var (rom, entry) = WriteRom();

            var result = VbiosResolver.ResolveFromRom(rom, GpuProfiles.Detect(Name, Omen), "fixture", Omen);

            result.Resolved.Should().BeTrue(result.Reason);
            result.StockMaxW.Should().Be(115);
            result.ShadowOffset.Should().Be(entry + 0x15);
        }

        [Fact]
        public void AnotherBoard_DoesNotResolveTheSameRom()
        {
            var (rom, _) = WriteRom();

            var result = VbiosResolver.ResolveFromRom(rom, GpuProfiles.Detect(Name, OtherBoard), "fixture", OtherBoard);

            result.Resolved.Should().BeFalse("the 115 W stock belongs to the reviewed HP OMEN board only");
        }

        [Fact]
        public void ARomForADifferentGpuIsRefused()
        {
            var (rom, _) = WriteRom(pcirDevice: 0x2D19);

            VbiosResolver.ResolveFromRom(rom, GpuProfiles.Detect(Name, Omen), "fixture", Omen).Resolved.Should().BeFalse();
        }
    }
}
