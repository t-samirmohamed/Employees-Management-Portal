using System.Security.Claims;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Dtos.Clients;
using EmpoloyeeManagment.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace EmpoloyeeManagment.Endpoints;

public static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clients").WithTags("Clients").RequireAuthorization("TaskAssigner");

        group.MapGet("/", GetClientsAsync);
        group.MapGet("/{id:int}", GetClientByIdAsync);
        group.MapPost("/", CreateClientAsync).RequireAuthorization("ClientManager");
        group.MapPatch("/{id:int}", UpdateClientAsync).RequireAuthorization("ClientManager");
        group.MapPost("/{id:int}/locations", AddLocationAsync).RequireAuthorization("ClientManager");
        group.MapGet("/{id:int}/visit-stats", GetClientVisitStatsAsync);

        return app;
    }

    private static async Task<Ok<List<ClientListItemDto>>> GetClientsAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var query = db.Clients.AsNoTracking().AsQueryable();

        if (!HasFullClientAccess(principal))
        {
            var assignedClientId = await GetSupervisorAssignedClientIdAsync(principal, db);
            if (assignedClientId is null) return TypedResults.Ok(new List<ClientListItemDto>());
            query = query.Where(c => c.Id == assignedClientId);
        }

        var clients = await query
            .OrderBy(c => c.Name)
            .Select(c => new ClientListItemDto(c.Id, c.Name, c.Email, c.Contact))
            .ToListAsync();

        return TypedResults.Ok(clients);
    }

    private static async Task<Results<Ok<ClientDetailDto>, NotFound>> GetClientByIdAsync(int id, ClaimsPrincipal principal, AppDbContext db)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (client is null) return TypedResults.NotFound();

        if (!await CanViewClientAsync(id, principal, db)) return TypedResults.NotFound();

        var locations = await db.Locations.AsNoTracking()
            .Where(l => l.ClientId == id)
            .Select(l => new LocationDto(l.Id, l.Name, l.Email, l.Contact))
            .ToListAsync();

        return TypedResults.Ok(new ClientDetailDto(client.Id, client.Name, client.Email, client.Contact, client.CreatedAt, locations));
    }

    private static async Task<Created<ClientDetailDto>> CreateClientAsync(CreateClientRequest request, AppDbContext db)
    {
        // Client is saved first to get its generated Id, then Locations are created
        // referencing it — the same save-parent-then-children shape as the (not yet
        // built) Visit+TaskItem creation designed in the visits story.
        await using var transaction = await db.Database.BeginTransactionAsync();

        var client = new Client
        {
            Name = request.Name,
            Email = request.Email,
            Contact = request.Contact,
            CreatedAt = DateTime.UtcNow
        };
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var locations = (request.Locations ?? [])
            .Select(l => new Location
            {
                ClientId = client.Id,
                Name = l.Name,
                Email = l.Email,
                Contact = l.Contact,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        db.Locations.AddRange(locations);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var locationDtos = locations.Select(l => new LocationDto(l.Id, l.Name, l.Email, l.Contact)).ToList();
        var detail = new ClientDetailDto(client.Id, client.Name, client.Email, client.Contact, client.CreatedAt, locationDtos);

        return TypedResults.Created($"/api/clients/{client.Id}", detail);
    }

    private static async Task<Results<Ok<ClientListItemDto>, NotFound>> UpdateClientAsync(
        int id, UpdateClientRequest request, AppDbContext db)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id);
        if (client is null) return TypedResults.NotFound();

        client.Name = request.Name;
        client.Email = request.Email;
        client.Contact = request.Contact;
        await db.SaveChangesAsync();

        return TypedResults.Ok(new ClientListItemDto(client.Id, client.Name, client.Email, client.Contact));
    }

    private static async Task<Results<Created<LocationDto>, NotFound>> AddLocationAsync(
        int id, CreateLocationRequest request, AppDbContext db)
    {
        if (!await db.Clients.AnyAsync(c => c.Id == id)) return TypedResults.NotFound();

        var location = new Location
        {
            ClientId = id,
            Name = request.Name,
            Email = request.Email,
            Contact = request.Contact,
            CreatedAt = DateTime.UtcNow
        };
        db.Locations.Add(location);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/api/clients/{id}", new LocationDto(location.Id, location.Name, location.Email, location.Contact));
    }

    private static async Task<Results<Ok<ClientVisitStatsDto>, NotFound, BadRequest<string>>> GetClientVisitStatsAsync(
        int id, string range, ClaimsPrincipal principal, AppDbContext db)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (client is null) return TypedResults.NotFound();

        if (!await CanViewClientAsync(id, principal, db)) return TypedResults.NotFound();

        var cutoff = GetRangeCutoff(range);
        if (cutoff is null)
        {
            return TypedResults.BadRequest("range must be one of: month, 3month, 6month, year.");
        }

        var visitCount = await db.Visits.CountAsync(v => v.ClientId == id && v.DateTime >= cutoff);

        return TypedResults.Ok(new ClientVisitStatsDto(id, range, visitCount));
    }

    private static DateTime? GetRangeCutoff(string range) => range switch
    {
        "month" => DateTime.UtcNow.AddMonths(-1),
        "3month" => DateTime.UtcNow.AddMonths(-3),
        "6month" => DateTime.UtcNow.AddMonths(-6),
        "year" => DateTime.UtcNow.AddYears(-1),
        _ => null
    };

    private static bool HasFullClientAccess(ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Role.Admin)) || principal.IsInRole(nameof(Role.Manager));

    private static async Task<bool> CanViewClientAsync(int clientId, ClaimsPrincipal principal, AppDbContext db)
    {
        if (HasFullClientAccess(principal)) return true;
        var assignedClientId = await GetSupervisorAssignedClientIdAsync(principal, db);
        return assignedClientId == clientId;
    }

    private static async Task<int?> GetSupervisorAssignedClientIdAsync(ClaimsPrincipal principal, AppDbContext db)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return null;

        var assignedLocationId = await db.Employees
            .Where(e => e.UserId == userId)
            .Select(e => e.AssignedLocationId)
            .FirstOrDefaultAsync();

        if (assignedLocationId is null) return null;

        return await db.Locations
            .Where(l => l.Id == assignedLocationId)
            .Select(l => (int?)l.ClientId)
            .FirstOrDefaultAsync();
    }
}
