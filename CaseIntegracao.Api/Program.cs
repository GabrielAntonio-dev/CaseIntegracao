using CaseIntegracao.Api.Workers;
using CaseIntegracao.Core;
using CaseIntegracao.Core.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RetryOptions>(builder.Configuration.GetSection(RetryOptions.SectionName));
builder.Services.AddCore(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddHostedService<RetryBackgroundService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Case Integração — Pedidos → CRM",
        Version = "v1",
        Description = "Recebe webhooks de pedidos, sincroniza com CRM mock e garante idempotência, ordenação e retry."
    });
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Case Integração v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();

app.Run();

public partial class Program;
