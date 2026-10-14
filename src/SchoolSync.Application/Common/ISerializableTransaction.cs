namespace SchoolSync.Application.Common;

/// <summary>
/// CT-701 / U29. Runs a unit of work under the strictest isolation the store offers, so a
/// check-then-write sequence cannot interleave with another one.
///
/// This exists for exactly one class of rule: an invariant over an AGGREGATE that no database
/// constraint can express. 04-data-model.md §4's concurrency policy resolves its four races with
/// unique indexes, because each is about a single row's identity. The learner-capacity gate
/// (BRL-073) is not like that — "COUNT(active students) &lt;= plan capacity" cannot be written as a
/// constraint, so the count and the insert must be serialised instead, or two concurrent
/// admissions at capacity &#8722; 1 both observe room and both succeed.
/// </summary>
public interface ISerializableTransaction
{
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
