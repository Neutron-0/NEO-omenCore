using System.Reflection;
using FluentAssertions;
using OmenCore.Hardware;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Hardware
{
    public class AmdGpuServiceTests
    {
        [Theory]
        [InlineData(0, "OK")]
        [InlineData(-8, "Not supported by this adapter/driver")]
        [InlineData(-3, "Invalid parameter")]
        [InlineData(-9999, "Unknown ADL error code")]
        public void DescribeAdlResult_MapsKnownCodesToReadableReasons(int code, string expectedSubstring)
        {
            using var logging = new LoggingService();
            logging.Initialize();
            using var service = new AmdGpuService(logging);

            var method = typeof(AmdGpuService).GetMethod("DescribeAdlResult",
                BindingFlags.NonPublic | BindingFlags.Static);
            method.Should().NotBeNull();

            var description = (string)method!.Invoke(null, new object[] { code })!;
            description.Should().Be(expectedSubstring);
        }
    }
}
