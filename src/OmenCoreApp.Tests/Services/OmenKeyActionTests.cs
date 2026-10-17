using FluentAssertions;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class OmenKeyActionTests
    {
        [Fact]
        public void ParseAction_MapsToggleOsd()
        {
            OmenKeyService.ParseAction("ToggleOsd").Should().Be(OmenKeyAction.ToggleOsd);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("NotAnAction")]
        public void ParseAction_UnknownFallsBackToQuickPopup(string? value)
        {
            OmenKeyService.ParseAction(value).Should().Be(OmenKeyAction.ShowQuickPopup);
        }

        [Fact]
        public void ToggleOsd_RaisesOnlyTheOsdEvent()
        {
            using var logging = new LoggingService();
            var svc = new OmenKeyService(logging);
            int osd = 0, popup = 0, window = 0;
            svc.ToggleOsdRequested += (_, _) => osd++;
            svc.ShowQuickPopupRequested += (_, _) => popup++;
            svc.ToggleOmenCoreRequested += (_, _) => window++;

            svc.Dispatch(OmenKeyAction.ToggleOsd);

            osd.Should().Be(1);
            popup.Should().Be(0);
            window.Should().Be(0);
        }
    }
}
