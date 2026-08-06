using Insgram_Manager.Components;
using Insgram_Manager.Data;
using Insgram_Manager.Extensions;
using Insgram_Manager.Hubs;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
 
// --- Serilog ---
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// --- Blazor ---
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// --- Ant Design Blazor ---
builder.Services.AddAntDesign();

// --- Entity Framework Core (SQLite) ---
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- SignalR ---
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 100 * 1024 * 1024; // 100MB — matches Blazor circuit limit
});

// --- Application Services ---
builder.Services.AddApplication();

// --- Infrastructure (Persistence, FileSystem, ZIP, HTML, Options, AutoMapper, FluentValidation) ---
builder.Services.AddInfrastructure(builder.Configuration);

// --- Import Module (Phase 2 forward declarations) ---
builder.Services.AddImport();

// --- Communication Module (Phase 6 — Instagram Communication Engine) ---
builder.Services.AddCommunication();

// --- Authentication Module (Playwright-based Instagram Browser Authentication) ---
builder.Services.AddAuthentication(builder.Configuration);

// --- Background Worker (Phase 5 — Automation Engine) ---
builder.Services.AddHostedService<Insgram_Manager.BackgroundJobs.BackgroundJobWorker>();

var app = builder.Build();

// --- Database Initialization ---
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
}

// --- HTTP Pipeline ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();
 
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// --- SignalR Hub Endpoint ---
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
