using ProzorroAnalytics.Api.Background;
using ProzorroAnalytics.Api.Endpoints;
using ProzorroAnalytics.Api.Integration.Prozorro;
using ProzorroAnalytics.Api.Parsers;
using ProzorroAnalytics.Api.Persistence;
using ProzorroAnalytics.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ProzorroOptions>(builder.Configuration.GetSection(ProzorroOptions.SectionName));
builder.Services.AddSingleton<TenderRepository>();
builder.Services.AddSingleton<TenderParser>();
builder.Services.AddSingleton<ImportService>();
builder.Services.AddSingleton<ImportJobState>();
builder.Services.AddSingleton<ImportJobQueue>();
builder.Services.AddSingleton<IImportJobHandler, ImportJobHandler>();
builder.Services.AddHostedService<ImportBackgroundService>();

builder.Services.AddHttpClient<ProzorroClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProzorroOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("ProzorroAnalyticsTestTask/1.0");
})
.AddStandardResilienceHandler();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapImportEndpoints();
app.MapAnalyticsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
