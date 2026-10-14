using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SchoolSync.Application.Common;

namespace SchoolSync.Infrastructure.Persistence;

/// <summary>
/// CT-701 / U29. SERIALIZABLE is what makes the learner-capacity gate safe: under it SQL Server
/// takes range locks for the COUNT, so a second admission for the same institution cannot insert
/// into the range this transaction counted until this one commits.
///
/// SERIALIZABLE buys that phantom-safety with deadlocks, and they are not hypothetical — eight
/// concurrent admissions at the last seat produced one immediately. Two readers hold range S locks
/// on the same range and each then wants an X lock to insert, so SQL Server picks a victim
/// (error 1205). A victim is NOT a business answer: the caller asked whether there was room, and
/// "you were deadlocked" is not an answer to that. So the victim is retried here.
///
/// The retry lives in this class rather than in <c>EnableRetryOnFailure</c> on the
/// <c>UseSqlServer</c> call because that would change retry behaviour for every query in the
/// product, which is a deployment-wide decision belonging to 12-infrastructure-spec.md and its
/// owner — not something to enable as a side effect of closing U29.
/// </summary>
public class SerializableTransaction(SchoolSyncDbContext db) : ISerializableTransaction
{
    private const int SqlDeadlockVictim = 1205;
    private const int MaxAttempts = 5;

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        // A test host on the InMemory provider has no transactions at all; the gate still runs, it
        // simply is not serialised there. Real deployments and the CT-701 performance host are on
        // SQL Server, where it is — which is why U29's proof is a real-SQL-Server test.
        //
        // The guard asks IsSqlServer(), NOT IsRelational(). IsRelational() answers true on the
        // InMemory test hosts: they call AddDbContext a second time to swap the provider, which
        // leaves the SqlServer extension in the options even though InMemory is what actually runs.
        // IsRelational() therefore describes the options, and IsSqlServer() the provider in use.
        if (!db.Database.IsSqlServer() || db.Database.CurrentTransaction is not null)
            return await work(ct);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var strategy = db.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using IDbContextTransaction transaction =
                        await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                    var result = await work(ct);
                    await transaction.CommitAsync(ct);
                    return result;
                });
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsDeadlockVictim(ex))
            {
                // Drop the entities this attempt tracked, or the retry re-sends the rolled-back
                // insert on top of a context that still believes it is Added.
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(20 * attempt), ct);
            }
        }
    }

    private static bool IsDeadlockVictim(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is SqlException { Number: SqlDeadlockVictim }) return true;
        return false;
    }
}
