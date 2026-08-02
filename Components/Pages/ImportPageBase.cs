using AntDesign;
using Insgram_Manager.Components.Shared;
using Insgram_Manager.Import.Models;
using Insgram_Manager.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.SignalR.Client;

namespace Insgram_Manager.Components.Pages;

/// <summary>
/// Code-behind for the Import page.
/// Handles ZIP upload, import progress tracking via SignalR, and result display.
/// All business logic is delegated to IImportService.
/// </summary>
public class ImportPageBase : BaseComponent, IAsyncDisposable
{
    [Inject]
    private Insgram_Manager.Interfaces.IImportService ImportService { get; set; } = default!;

    protected bool IsImporting { get; set; }
    protected bool HasResult { get; set; }
    protected ImportResult LastResult { get; set; } = new();
    protected string ResultStatus { get; set; } = string.Empty;
    protected int ProgressPercent { get; set; }
    protected string ProgressStatusText { get; set; } = string.Empty;
    protected ProgressStatus ProgressStatus { get; set; } = ProgressStatus.Active;

    private HubConnection? _hubConnection;

    protected override async Task OnInitializedAsync()
    {
        await SetupSignalRConnectionAsync();
    }

    private async Task SetupSignalRConnectionAsync()
    {
        var hubUrl = NavigationManager.ToAbsoluteUri("/hubs/notifications");

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<object>("ImportStarted", async (data) =>
        {
            ProgressStatusText = "Import started...";
            ProgressPercent = 0;
            ProgressStatus = ProgressStatus.Active;
            await InvokeAsync(StateHasChanged);
        });

        _hubConnection.On<object>("ImportProgress", async (data) =>
        {
            ProgressStatusText = "Processing files...";
            ProgressPercent = Math.Min(90, ProgressPercent + 10);
            ProgressStatus = ProgressStatus.Active;
            await InvokeAsync(StateHasChanged);
        });

        _hubConnection.On<object>("ImportCompleted", async (data) =>
        {
            ProgressStatusText = "Import completed!";
            ProgressPercent = 100;
            ProgressStatus = ProgressStatus.Success;
            await InvokeAsync(StateHasChanged);
        });

        _hubConnection.On<object>("ImportFailed", async (data) =>
        {
            ProgressStatusText = "Import failed.";
            ProgressStatus = ProgressStatus.Exception;
            await InvokeAsync(StateHasChanged);
        });

        try
        {
            await _hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            LogService.LogWarning("Failed to connect to SignalR hub: {Message}", ex.Message);
        }
    }

    protected async Task HandleFileChange(InputFileChangeEventArgs e)
    {
        if (IsImporting)
            return;

        var file = e.File;
        var extension = Path.GetExtension(file.Name);
        if (!string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase))
        {
            MessageService.Error("Please select a ZIP file.");
            return;
        }

        await using var stream = file.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024);
        await ProcessImportAsync(file.Name, stream);
    }

    protected void Reset()
    {
        HasResult = false;
        IsImporting = false;
        ProgressPercent = 0;
        ProgressStatusText = string.Empty;
        LastResult = new();
        StateHasChanged();
    }

    private async Task ProcessImportAsync(string fileName, Stream stream)
    {
        if (IsImporting)
            return;

        IsImporting = true;
        HasResult = false;
        ProgressPercent = 0;
        ProgressStatusText = "Uploading...";
        ProgressStatus = ProgressStatus.Active;
        StateHasChanged();

        try
        {
            var request = new ImportRequest
            {
                FileName = fileName,
                FileStream = stream,
                Format = ImportFormat.Html
            };

            LastResult = await ImportService.ImportAsync(request);
            ResultStatus = LastResult.Success ? "Success" : "Completed with errors";
            HasResult = true;

            if (LastResult.Success)
            {
                MessageService.Success($"Imported {LastResult.ImportedRecords} records successfully.");
            }
            else if (LastResult.Errors.Count > 0)
            {
                MessageService.Warning($"Import completed with {LastResult.Errors.Count} error(s).");
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("Import failed", ex);
            MessageService.Error($"Import failed: {ex.Message}");
            HasResult = true;
            LastResult = new ImportResult
            {
                Success = false,
                Errors = [$"Import failed: {ex.Message}"],
                Message = ex.Message
            };
            ResultStatus = "Failed";
        }
        finally
        {
            IsImporting = false;
            StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
