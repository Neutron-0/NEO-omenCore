using System;
using System.IO;
using FluentAssertions;
using NvpwrControlBlackwell;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    /// <summary>Resolve VBIOS looks for a user-supplied nvflash. RTX 50 cards use nvflashb.exe (Prophecy standalone 1.2.0 notes).</summary>
    public class NvflashLookupTests
    {
        private const string Base = @"C:\App";

        [Fact]
        public void FindsNvflashb_InTheToolsFolder()
        {
            var expected = Path.Combine(Base, "tools", "nvflashb.exe");

            var found = VbiosResolver.FindNvflash(Base, "", p => p == expected);

            found.Should().Be(Path.GetFullPath(expected));
        }

        [Fact]
        public void FindsNvflashb_OnThePath()
        {
            var expected = Path.Combine(@"D:\Tools", "nvflashb.exe");

            var found = VbiosResolver.FindNvflash(Base, @"C:\Windows;D:\Tools", p => p == expected);

            found.Should().Be(Path.GetFullPath(expected));
        }

        [Fact]
        public void StillFindsTheClassicName_AndPrefersIt()
        {
            var classic = Path.Combine(Base, "nvflash64.exe");
            var blackwell = Path.Combine(Base, "nvflashb.exe");

            VbiosResolver.FindNvflash(Base, "", p => p == classic || p == blackwell).Should().Be(Path.GetFullPath(classic));
        }

        [Fact]
        public void ReturnsNull_WhenNeitherIsPresent()
        {
            VbiosResolver.FindNvflash(Base, @"C:\Windows", _ => false).Should().BeNull();
        }
    }
}
