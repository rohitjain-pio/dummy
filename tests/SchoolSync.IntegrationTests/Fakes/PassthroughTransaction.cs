using SchoolSync.Application.Common;

namespace SchoolSync.IntegrationTests.Fakes;

/// <summary>
/// CT-701 / U29. For unit-level service tests that use fake repositories and have no database at
/// all, so there is nothing to serialise. It runs the work directly — deliberately NOT a stand-in
/// for the real isolation, which is why the U29 race is proven against real SQL Server in
/// <c>CapacitySerialisationTests</c> rather than here.
/// </summary>
public sealed class PassthroughTransaction : ISerializableTransaction
{
    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
}
