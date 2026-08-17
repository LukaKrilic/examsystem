using ExamSystem.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace ExamSystem.Web.Data;

// EF maps to the schema owned by database/Database.sql. It NEVER creates or migrates it:
// no EnsureCreated, no Migrations, no Database.Migrate(). It is purely a mapper.
public class ExamDbContext(DbContextOptions<ExamDbContext> options) : DbContext(options)
{
    public DbSet<ExamAccessCode> ExamAccessCodes => Set<ExamAccessCode>();
    public DbSet<Instruction> Instructions => Set<Instruction>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<SessionOutcome> SessionOutcomes => Set<SessionOutcome>();
    public DbSet<LockedExam> LockedExams => Set<LockedExam>();
    public DbSet<Screenshot> Screenshots => Set<Screenshot>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Tables are PascalCase and SINGULAR, matching the entity class names. EF's default would use the
        // pluralized DbSet name, so force singular in one loop — then every property maps with zero config.
        foreach (var entity in mb.Model.GetEntityTypes())
            entity.SetTableName(entity.ClrType.Name);

        mb.Entity<ExamSession>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>();       // enum ↔ 'ACTIVE' / 'FINISHED' / 'AUTO_CLOSED'
            e.Property(x => x.WizardStep).HasConversion<string>();   // enum ↔ 'EXAMS' / ... / 'IN_EXAM'
        });
    }
}
