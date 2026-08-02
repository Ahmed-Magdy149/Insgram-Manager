using Microsoft.AspNetCore.Components;

namespace Insgram_Manager.Interfaces;

/// <summary>
/// Abstraction for dialog/modal interactions.
/// Wraps AntDesign's IModalService to decouple components from the UI library.
/// </summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string content);

    Task<bool> ConfirmAsync(string title, string content, string okText, string cancelText);

    Task ShowModalAsync<TComponent>(string title) where TComponent : ComponentBase;
}
