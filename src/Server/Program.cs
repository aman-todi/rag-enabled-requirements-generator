using Microsoft.AspNetCore.HttpOverrides;
using RequirementsGenerator.Server.Options;
using RequirementsGenerator.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services
    .AddOptions<FoundryOptions>()
    .Bind(builder.Configuration.GetSection("Foundry"));

builder.Services.AddSingleton<IFoundryChatService, FoundryChatService>();

// App Service (and any load balancer / App Gateway / Front Door in front of it) terminates TLS
// and forwards the original client info via these headers.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Serves the React production build (see src/client, built into wwwroot/ by `dotnet publish`).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

// SPA client-side routing fallback: any non-API, non-static-file GET request serves index.html.
app.MapFallbackToFile("index.html");

app.Run();
