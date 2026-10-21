using Microsoft.EntityFrameworkCore;
using hrms.Models;

namespace hrms.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();

    public DbSet<MainRole> MainRoles => Set<MainRole>();
    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CompanyRole> CompanyRoles => Set<CompanyRole>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<EmployeeHistory> EmployeeHistories => Set<EmployeeHistory>();

    public DbSet<ReportingManager> ReportingManagers => Set<ReportingManager>();

    public DbSet<PermissionHeading> PermissionHeadings => Set<PermissionHeading>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<EmployeePermission> EmployeePermissions => Set<EmployeePermission>();

    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<AttendanceSetting> AttendanceSettings => Set<AttendanceSetting>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<AttendanceRequest> AttendanceRequests => Set<AttendanceRequest>();
    public DbSet<MainEmployee> MainEmployees => Set<MainEmployee>();
    public DbSet<MainPermissionHeading> MainPermissionHeadings => Set<MainPermissionHeading>();
    public DbSet<MainPermission> MainPermissions => Set<MainPermission>();
    public DbSet<MainEmployeePermission> MainEmployeePermissions => Set<MainEmployeePermission>();
    public DbSet<ReportingExclusion> ReportingExclusions => Set<ReportingExclusion>();

    public DbSet<LeaveSetting> LeaveSettings => Set<LeaveSetting>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveDay> LeaveDays => Set<LeaveDay>();
    public DbSet<LeaveApproval> LeaveApprovals => Set<LeaveApproval>();
    public DbSet<AttendanceCheckin> AttendanceCheckins => Set<AttendanceCheckin>();
    public DbSet<LoginLocation> LoginLocations => Set<LoginLocation>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<WorkTaskFile> WorkTaskFiles => Set<WorkTaskFile>();
    public DbSet<TaskSetting> TaskSettings => Set<TaskSetting>();
    public DbSet<TaskApproval> TaskApprovals => Set<TaskApproval>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<TaskAssignmentTarget> TaskAssignmentTargets => Set<TaskAssignmentTarget>();
    public DbSet<Notification> Notifications => Set<Notification>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<User>(entity =>
       {
           entity.HasIndex(u => u.Email).IsUnique();
           entity.HasIndex(u => u.PhoneNumber).IsUnique();

           entity.HasIndex(u => u.PhoneNumber, "ix_users_phone_trgm")
                 .HasMethod("gin")
                 .HasOperators("gin_trgm_ops");
           entity.Property(u => u.Name).HasMaxLength(150).IsRequired();
           entity.Property(u => u.Email).HasMaxLength(200).IsRequired();
           entity.Property(u => u.PhoneNumber).HasMaxLength(20);

           entity.HasOne(u => u.MainRole)
                 .WithMany(m => m.Users)
                 .HasForeignKey(u => u.MainRoleId)
                 .OnDelete(DeleteBehavior.SetNull);

           entity.HasOne(u => u.Role)
                 .WithMany(r => r.Users)
                 .HasForeignKey(u => u.RoleId)
                 .OnDelete(DeleteBehavior.SetNull);
       });

        modelBuilder.Entity<MainRole>(entity =>
        {
            entity.HasIndex(m => m.Slug).IsUnique();
            entity.Property(m => m.Name).HasMaxLength(100).IsRequired();
            entity.Property(m => m.Slug).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(r => r.Slug).IsUnique();
            entity.Property(r => r.Name).HasMaxLength(100).IsRequired();
            entity.Property(r => r.Slug).HasMaxLength(100).IsRequired();
        });

        // inside OnModelCreating
        modelBuilder.Entity<ReportingManager>(b =>
        {
            // deleting a user removes their links (as person and as manager)
            b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.ManagerUser).WithMany().HasForeignKey(x => x.ManagerUserId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ReportingExclusion>(b =>
{
    b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    b.HasOne(x => x.ManagerUser).WithMany().HasForeignKey(x => x.ManagerUserId).OnDelete(DeleteBehavior.Cascade);
});
        modelBuilder.Entity<MainEmployee>()
    .HasOne(x => x.Role).WithMany()
    .HasForeignKey(x => x.RoleId)
    .OnDelete(DeleteBehavior.Restrict);
    }


}