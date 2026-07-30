using Xunit;

// Each test class owns an ApiFactory that boots its own Postgres + Redis containers and drives the API
// over the loopback address. Running the classes in parallel contends for container startup and shares the
// per-IP rate-limit partition, so serialize the assembly's collections.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
