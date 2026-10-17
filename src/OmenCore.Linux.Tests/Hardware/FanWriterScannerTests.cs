using FluentAssertions;
using OmenCore.Linux.Hardware;

namespace OmenCore.Linux.Tests.Hardware;

public class FanWriterScannerTests : IDisposable
{
    private readonly string _proc = Path.Combine(Path.GetTempPath(), "omencore-proc-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_proc, true); } catch (IOException) { }
    }

    [Fact]
    public void Match_FindsKnownWriters_AndIgnoresOtherProcesses()
    {
        var hits = FanWriterScanner.Match(new[] { "systemd", "victus-fan", "bash", "omenctld" });

        hits.Should().Equal("victus-fan", "omenctld");
    }

    [Fact]
    public void Match_HandlesTheKernelsFifteenCharacterNameLimit()
    {
        FanWriterScanner.Match(new[] { "omen-fan-contro" }).Should().Equal("omen-fan-control");
    }

    [Fact]
    public void Match_NoWriters_IsEmpty()
    {
        FanWriterScanner.Match(new[] { "systemd", "sshd" }).Should().BeEmpty();
    }

    [Fact]
    public void Scan_ReadsCommFilesUnderProc_AndSkipsNonProcessEntries()
    {
        Directory.CreateDirectory(Path.Combine(_proc, "4242"));
        File.WriteAllText(Path.Combine(_proc, "4242", "comm"), "victus-backend\n");
        Directory.CreateDirectory(Path.Combine(_proc, "self-not-a-pid"));
        Directory.CreateDirectory(Path.Combine(_proc, "17")); // exited: no comm file

        FanWriterScanner.Scan(_proc).Should().Equal("victus-backend");
    }

    [Fact]
    public void Scan_MissingProc_IsEmpty()
    {
        FanWriterScanner.Scan(Path.Combine(_proc, "nope")).Should().BeEmpty();
    }
}
