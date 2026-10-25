using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Common.Validation;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Features.Harvests;
using HouseholdFarm.Api.Features.Reports.Finance;
using HouseholdFarm.Api.Features.Sales;
using HouseholdFarm.Api.Features.Seasons;
using HouseholdFarm.Api.Features.Supplies;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Đăng ký Serilog qua DI thay vì logger tĩnh, để nhiều host trong cùng lượt test không giành nhau một logger toàn cục.
builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(builder.Environment.ContentRootPath, "logs", "log-.txt"),
        rollingInterval: RollingInterval.Day));

builder.Services
    .AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
        options.Filters.Add<ResponseEnvelopeFilter>();
        // Không tự coi thuộc tính không nullable là bắt buộc: validator là nơi duy nhất báo thiếu, bằng tiếng Việt.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions))
    // ValidationFilter tự trả VALIDATION_ERROR cho lỗi đọc dữ liệu, thay cho phản hồi 400 mặc định của [ApiController].
    .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true);

// ProblemDetails và tài liệu OpenAPI dùng cấu hình JSON riêng, tách khỏi cấu hình JSON của controller.
builder.Services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = AppProblemDetails.Customize);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddScoped<IWarningCollector, WarningCollector>();

// Thông báo mặc định bằng tiếng Việt cho quy tắc nào quên WithMessage.
ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("vi");
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddOpenApi();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAppClock, AppClock>();

// Lớp tạm cho tới FR-AUTH-02, khi người dùng hiện tại được nạp từ database mỗi yêu cầu.
builder.Services.AddScoped<ICurrentUser, AnonymousCurrentUser>();

// Chuỗi kết nối đọc lúc tạo DbContext, không đọc lúc khởi động, để test thay được bằng database của Testcontainers.
// Không bật EnableRetryOnFailure: cơ chế tự thử lại không chạy được với giao dịch database mở bằng tay.
builder.Services.AddScoped<CommonColumnsInterceptor>();
builder.Services.AddDbContext<AppDbContext>((services, options) => options
    .UseNpgsql(services.GetRequiredService<IConfiguration>().GetConnectionString("Default"))
    .UseSnakeCaseNamingConvention()
    .AddInterceptors(services.GetRequiredService<CommonColumnsInterceptor>()));

// Scoped để dùng chung AppDbContext với service gọi nó: nhật ký được lưu cùng lần SaveChangesAsync của thao tác (BR-GEN-07).
builder.Services.AddScoped<IAuditLogger, AuditLogger>();

// Điểm nối giữa các module: scoped để bên gọi và bên được gọi dùng chung một AppDbContext, một giao dịch database.
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<ISaleHarvestLinkService, SaleHarvestLinkService>();
builder.Services.AddScoped<ISeasonFinanceService, SeasonFinanceService>();
builder.Services.AddScoped<IProduceStockService, ProduceStockService>();
builder.Services.AddScoped<ISeasonStateGuard, SeasonStateGuard>();

var app = builder.Build();

// Chỉ ghi phương thức, đường dẫn, mã trả về và thời gian; không ghi query string vì có thể chứa số điện thoại khi tìm kiếm.
app.UseSerilogRequestLogging();

// Đặt sau ghi log yêu cầu để log ghi đúng mã HTTP cuối cùng, không ghi lỗi hai lần.
app.UseExceptionHandler();
// Lỗi framework trả về không có body (route không khớp, xác thực từ chối) cũng thành ProblemDetails.
app.UseStatusCodePages();

// Tài liệu API và Swagger UI chỉ mở ở môi trường phát triển.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "v1"));
}

app.MapControllers();

app.Run();

// Tên trường camelCase và DateOnly dạng yyyy-MM-dd là mặc định của cấu hình JSON cho web.
static void ConfigureJson(JsonSerializerOptions options) =>
    // Enum gửi, nhận dạng chuỗi đúng tên enum; không nhận số để giá trị luôn đọc được.
    options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));

// Cho phép project test dựng ứng dụng bằng WebApplicationFactory<Program>.
public partial class Program;
