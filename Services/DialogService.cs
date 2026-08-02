using AntDesign;
using Insgram_Manager.Interfaces;
using Microsoft.AspNetCore.Components;

namespace Insgram_Manager.Services;

/// <summary>
/// Dialog service that wraps AntDesign's ModalService for
/// confirmation dialogs and modal component rendering.
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly ModalService _modalService;

    public DialogService(ModalService modalService)
    {
        _modalService = modalService;
    }

    public async Task<bool> ConfirmAsync(string title, string content) =>
        await ConfirmAsync(title, content, "OK", "Cancel");

    public async Task<bool> ConfirmAsync(string title, string content, string okText, string cancelText)
    {
        return await _modalService.ConfirmAsync(new ConfirmOptions
        {
            Title = title,
            Content = content,
            OkText = okText,
            CancelText = cancelText
        });
    }

    public async Task ShowModalAsync<TComponent>(string title) where TComponent : ComponentBase
    {
        await _modalService.CreateModalAsync(new ModalOptions
        {
            Title = title,
            Content = builder => builder.OpenComponent<TComponent>(0)
        });
    }
}
