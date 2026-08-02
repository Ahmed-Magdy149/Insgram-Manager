using AutoMapper;

namespace Insgram_Manager.Mapping;

/// <summary>
/// Base AutoMapper profile for the Insgram Manager application.
/// Concrete mapping configurations will be added in Phase 2.
/// No mappings are defined yet — this establishes the profile structure only.
/// </summary>
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Entity <-> DTO mappings will be configured here in Phase 2.
        // Example:
        // CreateMap<Account, AccountDto>();
        // CreateMap<CreateAccountRequest, Account>();
    }
}
