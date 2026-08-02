using Insgram_Manager.Components;
using Insgram_Manager.Data;
using Insgram_Manager.Extensions;
using Insgram_Manager.Hubs;
using Insgram_Manager.Services;
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
builder.Services.AddSignalR();

// --- Application Services ---
builder.Services.AddApplicationServices();
builder.Services.AddScoped<IThemeService, ThemeService>();

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
