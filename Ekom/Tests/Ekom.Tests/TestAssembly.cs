using Xunit;

// ConfigurationScope replaces the process-wide Configuration.Resolver.
// Test classes must not restore each other's service providers while still in use.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
