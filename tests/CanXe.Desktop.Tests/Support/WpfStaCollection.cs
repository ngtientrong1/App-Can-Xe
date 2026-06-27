using CanXe.Desktop.Tests.Support;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CanXe.Desktop.Tests;

[CollectionDefinition("WpfSta", DisableParallelization = true)]
public sealed class WpfStaCollection : ICollectionFixture<WpfSmokeFixture>;
