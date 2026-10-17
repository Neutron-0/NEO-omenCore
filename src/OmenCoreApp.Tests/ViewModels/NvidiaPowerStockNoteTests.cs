using FluentAssertions;
using OmenCore.ViewModels;
using Xunit;

namespace OmenCoreApp.Tests.ViewModels
{
    public class NvidiaPowerStockNoteTests
    {
        [Fact]
        public void Mismatch_SaysBothNumbers_AndThatItIsNotAnErrorByItself()
        {
            // K.7: a laptop that reads 115 W while the profile expects 140 W.
            var note = NvidiaPowerViewModel.StockMismatchNote(115, 140);

            note.Should().Contain("Stock max read: 115 W").And.Contain("expected: 140 W").And.Contain("not an error by itself");
        }

        [Theory]
        [InlineData(140.0, 140)]
        [InlineData(140.4, 140)]
        public void Agreement_SaysNothing(double live, int expected)
        {
            NvidiaPowerViewModel.StockMismatchNote(live, expected).Should().BeEmpty();
        }

        [Fact]
        public void UnknownValues_SayNothing()
        {
            NvidiaPowerViewModel.StockMismatchNote(null, 140).Should().BeEmpty();
            NvidiaPowerViewModel.StockMismatchNote(115, null).Should().BeEmpty();
        }
    }
}
