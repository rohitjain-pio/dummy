namespace SchoolSync.IntegrationTests.Fakes;

// A settable TimeProvider. Needed because CFG-005's 7-day expiry cannot be tested honestly
// against the wall clock — "issued 8 days ago" (TC-307) and the exact boundary at 7 days are
// both statements about time passing, and a test that fakes them by back-dating the stored row
// only proves the comparison, never that the expiry window is 7 days rather than 5 or 30.
public class TestClock(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = start;

    public TestClock() : this(new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero)) { }

    public override DateTimeOffset GetUtcNow() => UtcNow;

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
