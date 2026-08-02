namespace Insgram_Manager.Domain.Enums;

/// <summary>
/// Represents the connection status of an Instagram account.
/// Prepared for Phase 2 account management.
/// </summary>
public enum AccountStatus
{
    Disconnected = 0,
    Connected = 1,
    Challenged = 2,
    Banned = 3
}
