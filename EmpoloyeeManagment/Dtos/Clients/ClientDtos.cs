namespace EmpoloyeeManagment.Dtos.Clients;

public record CreateLocationRequest(string Name, string Email, string Contact);

public record CreateClientRequest(string Name, string Email, string Contact, List<CreateLocationRequest>? Locations);

public record LocationDto(int Id, string Name, string Email, string Contact);

public record ClientListItemDto(int Id, string Name, string Email, string Contact);

public record ClientDetailDto(int Id, string Name, string Email, string Contact, DateTime CreatedAt, List<LocationDto> Locations);

public record ClientVisitStatsDto(int ClientId, string Range, int VisitCount);
