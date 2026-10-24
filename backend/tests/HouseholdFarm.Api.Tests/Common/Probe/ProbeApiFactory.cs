using FluentValidation;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HouseholdFarm.Api.Tests.Common.Probe;

// Ứng dụng thật có thêm ProbeController và validator của nó; các test khác không thấy controller này.
public sealed class ProbeApiFactory(PostgresFixture postgres) : ApiFactory(postgres)
{
    public CapturingLogger<GlobalExceptionHandler> ExceptionLogger { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
            services.AddValidatorsFromAssemblyContaining<ProbeController>();
            services.AddSingleton<ILogger<GlobalExceptionHandler>>(ExceptionLogger);
        });
    }
}
