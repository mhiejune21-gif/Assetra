using Microsoft.EntityFrameworkCore;
using Assetra.Models;

namespace Assetra.Data
{
    public class AssetraContext : DbContext
    {
        public AssetraContext(DbContextOptions<AssetraContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Property> Properties { get; set; }
        public DbSet<LendingRecord> LendingRecords { get; set; }
        public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
        public DbSet<ConditionHistory> ConditionHistories { get; set; }
        public DbSet<ConditionReport> ConditionReports { get; set; }
    }
}