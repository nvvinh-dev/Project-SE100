using System.Reflection;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Data;

// IdentityUserContext thay vì IdentityDbContext để không sinh các bảng vai trò của Identity; vai trò lưu ở cột role.
public class AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser currentUser)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    /// <summary>
    /// Tên Global Query Filter lọc theo hộ. Chỉ module ADM được bỏ qua bằng
    /// <c>IgnoreQueryFilters([AppDbContext.HouseholdFilter])</c>, và phải ghi nhật ký (BR-USR-16).
    /// </summary>
    public const string HouseholdFilter = "Household";

    private static readonly MethodInfo _configureHouseholdOwnedMethod = typeof(AppDbContext)
        .GetMethod(nameof(ConfigureHouseholdOwned), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public DbSet<Household> Households => Set<Household>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<SeasonAssignment> SeasonAssignments => Set<SeasonAssignment>();

    public DbSet<Plot> Plots => Set<Plot>();

    public DbSet<Crop> Crops => Set<Crop>();

    public DbSet<Season> Seasons => Set<Season>();

    public DbSet<SeasonSupplyPlan> SeasonSupplyPlans => Set<SeasonSupplyPlan>();

    public DbSet<SeasonNote> SeasonNotes => Set<SeasonNote>();

    public DbSet<FarmTask> FarmTasks => Set<FarmTask>();

    public DbSet<FarmTaskSupplyUsage> FarmTaskSupplyUsages => Set<FarmTaskSupplyUsage>();

    public DbSet<HarvestRecord> HarvestRecords => Set<HarvestRecord>();

    public DbSet<ProduceStockAdjustment> ProduceStockAdjustments => Set<ProduceStockAdjustment>();

    public DbSet<Supply> Supplies => Set<Supply>();

    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleHarvestLink> SaleHarvestLinks => Set<SaleHarvestLink>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<ReportExport> ReportExports => Set<ReportExport>();

    public DbSet<AIUsage> AIUsages => Set<AIUsage>();

    public DbSet<AISeasonSummary> AISeasonSummaries => Set<AISeasonSummary>();

    // EF đọc lại thuộc tính này ở mỗi truy vấn (không chụp giá trị lúc dựng model), nên filter luôn theo người của yêu cầu hiện tại.
    private Guid? CurrentHouseholdId => currentUser.HouseholdId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Identity tự đặt tên bảng AspNet... bằng ToTable; quy ước snake_case không ghi đè tên đặt tay,
        // nên đặt lại cho khớp tên bảng trong data model. Cột và khóa vẫn do quy ước sinh;
        // hai chỉ mục UserNameIndex, EmailIndex giữ tên của Identity.
        modelBuilder.Entity<ApplicationUser>().ToTable("asp_net_users");
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("asp_net_user_claims");
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("asp_net_user_logins");
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("asp_net_user_tokens");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var clrType in modelBuilder.Model.GetEntityTypes().Where(e => !e.IsOwned()).Select(e => e.ClrType).ToList())
        {
            if (typeof(IHouseholdOwned).IsAssignableFrom(clrType))
            {
                _configureHouseholdOwnedMethod.MakeGenericMethod(clrType).Invoke(this, [modelBuilder]);
            }

            if (typeof(IAuditable).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).HasOne(typeof(ApplicationUser)).WithMany().HasForeignKey(nameof(IAuditable.CreatedById));
            }

            if (typeof(IVersioned).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).HasOne(typeof(ApplicationUser)).WithMany().HasForeignKey(nameof(IVersioned.UpdatedById));
                modelBuilder.Entity(clrType).Property(nameof(IVersioned.Version)).IsRowVersion();
            }

            if (typeof(IVoidable).IsAssignableFrom(clrType))
            {
                modelBuilder.Entity(clrType).HasOne(typeof(ApplicationUser)).WithMany().HasForeignKey(nameof(IVoidable.VoidedById));
            }
        }

        // audit_logs không mang IHouseholdOwned vì household_id để trống được, nên cấu hình filter riêng.
        // Vế != null bắt buộc: EF so sánh null theo kiểu C# (null bằng null), thiếu vế này thì tài khoản không thuộc hộ nào
        // sẽ thấy các dòng nhật ký không có hộ.
        modelBuilder.Entity<AuditLog>()
            .HasQueryFilter(HouseholdFilter, a => a.HouseholdId != null && a.HouseholdId == CurrentHouseholdId);

        // Dữ liệu nghiệp vụ không bao giờ xóa dây chuyền: xóa nhầm một dòng cha không được kéo theo cả lịch sử giao dịch.
        // Chỉ áp cho entity của dự án; các bảng phụ của Identity giữ cấu hình gốc.
        var foreignKeys = modelBuilder.Model.GetEntityTypes()
            .Where(e => e.ClrType.Assembly == typeof(AppDbContext).Assembly)
            .SelectMany(e => e.GetDeclaredForeignKeys())
            .Where(fk => !fk.IsOwnership);
        foreach (var foreignKey in foreignKeys)
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    // BR-GEN-01: mọi bảng nghiệp vụ chỉ thấy dữ liệu của hộ người đang đăng nhập; chưa đăng nhập thì không thấy gì.
    private void ConfigureHouseholdOwned<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IHouseholdOwned
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.HasOne<Household>().WithMany().HasForeignKey(nameof(IHouseholdOwned.HouseholdId));
        entity.HasQueryFilter(HouseholdFilter, e => e.HouseholdId == CurrentHouseholdId);
    }
}
