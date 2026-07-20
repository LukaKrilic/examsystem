using ExamSystem.Web.Domain;
using ExamSystem.Web.Enums;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Tests;

// Counts seed rows through EVERY DbSet. Because each assertion materializes the full column list,
// a mismatch between an entity and database/Database.sql surfaces here first (the drift alarm).
[Collection(ExamSystemTestCollection.Name)]
public class SeedSmokeTests(ExamSystemTestFixture fixture)
{
    [Fact]
    public async Task Every_DbSet_maps_to_the_schema_and_seed_row_counts_match()
    {
        await using var db = fixture.CreateContext();

        // Seeded tables — exact counts.
        Assert.Equal(3, (await db.Students.ToListAsync()).Count);
        Assert.Equal(2, (await db.Courses.ToListAsync()).Count);
        Assert.Equal(2, (await db.Exams.ToListAsync()).Count);
        Assert.Equal(2, (await db.ExamAccessCodes.ToListAsync()).Count);
        Assert.Equal(4, (await db.ExamRegistrations.ToListAsync()).Count);
        Assert.Equal(10, (await db.LearningOutcomes.ToListAsync()).Count);
        Assert.Equal(20, (await db.StudentOutcomePoints.ToListAsync()).Count);
        Assert.Equal(2, (await db.Instructions.ToListAsync()).Count);

        // Empty tables — the query still SELECTs every mapped column, so it catches drift at 0 rows.
        Assert.Empty(await db.Devices.ToListAsync());
        Assert.Empty(await db.ExamSessions.ToListAsync());
        Assert.Empty(await db.SessionOutcomes.ToListAsync());
        Assert.Empty(await db.LockedExams.ToListAsync());
        Assert.Empty(await db.Screenshots.ToListAsync());
    }

    [Fact]
    public async Task Two_exams_share_the_same_ExamDateTime_for_the_lockout_testbed()
    {
        await using var db = fixture.CreateContext();

        var times = await db.Exams.Select(e => e.ExamDateTime).Distinct().ToListAsync();

        Assert.Single(times);   // both EXAM-1001 and EXAM-1002 at the same instant
    }

    [Fact]
    public async Task At_least_one_outcome_sits_exactly_at_the_fifty_percent_green_boundary()
    {
        await using var db = fixture.CreateContext();

        var points = await db.StudentOutcomePoints.ToListAsync();

        // Comparison on decimal, never double — the boundary is >= 50%.
        Assert.Contains(points, p => p.TotalPointsEarned == 0.5m * p.TotalPointsMax);
    }

    [Fact]
    public async Task Session_enums_round_trip_through_the_string_conversion()
    {
        await using var db = fixture.CreateContext();

        // Insert an ExamSession with enum values and read it back to prove the string conversion
        // matches the DB's NVARCHAR columns ('FINISHED', 'IN_EXAM', ...). A transaction that never
        // commits keeps the shared DB pristine for the other tests, whatever order they run in.
        await using var tx = await db.Database.BeginTransactionAsync();

        db.ExamSessions.Add(new ExamSession
        {
            SessionId = "SESSION-SMOKE-1",
            StudentId = 1,
            ExamId = 1,
            Status = SessionStatus.FINISHED,
            WizardStep = WizardStep.IN_EXAM,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await db.ExamSessions.SingleAsync(s => s.SessionId == "SESSION-SMOKE-1");
        Assert.Equal(SessionStatus.FINISHED, loaded.Status);
        Assert.Equal(WizardStep.IN_EXAM, loaded.WizardStep);

        await tx.RollbackAsync();
    }
}
