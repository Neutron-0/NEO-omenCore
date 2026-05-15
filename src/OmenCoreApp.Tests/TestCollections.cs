using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = false)]

namespace OmenCoreApp.Tests
{
    [CollectionDefinition("Config Isolation", DisableParallelization = true)]
    public class ConfigIsolationCollectionDefinition { }

    [CollectionDefinition("NonParallel", DisableParallelization = true)]
    public class NonParallelCollectionDefinition { }

    [CollectionDefinition("STA Isolation", DisableParallelization = true)]
    public class StaIsolationCollectionDefinition { }
}
