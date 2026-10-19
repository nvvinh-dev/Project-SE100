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

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Chỉ ghi phương thức, đường dẫn, mã trả về và thời gian; không ghi query string vì có thể chứa số điện thoại khi tìm kiếm.
app.UseSerilogRequestLogging();

// Tài liệu API và Swagger UI chỉ mở ở môi trường phát triển.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "v1"));
}

app.MapControllers();

app.Run();

// Cho phép project test dựng ứng dụng bằng WebApplicationFactory<Program>.
public partial class Program;
