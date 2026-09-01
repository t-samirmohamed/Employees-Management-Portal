using System.Security.Claims;
using System.Text;
using EmpoloyeeManagment.Authorization;
using EmpoloyeeManagment.Data;
using EmpoloyeeManagment.Endpoints;
using EmpoloyeeManagment.Middleware;
using EmpoloyeeManagment.Models;
using EmpoloyeeManagment.OpenApi;
using EmpoloyeeManagment.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            // Makes "sign out" real: every authenticated request re-checks the user's
            // current Identity security stamp against the one baked into the token at
            // issue time, so rotating the stamp on logout revokes it immediately.
            OnTokenValidated = async context =>
            {
                var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var tokenStamp = context.Principal?.FindFirstValue("securityStamp");

                var user = userId is null ? null : await userManager.FindByIdAsync(userId);
                if (user is null)
                {
                    context.Fail("User no longer exists.");
                    return;
                }

                var currentStamp = await userManager.GetSecurityStampAsync(user);
                if (tokenStamp is null || currentStamp != tokenStamp)
                {
                    context.Fail("Token has been revoked.");
                }
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(nameof(Role.Admin)));
});
builder.Services.AddSingleton<IAuthorizationHandler, AdminAuthorizationHandler>();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("NextJsClient", policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod());
    // No AllowCredentials() — auth is a Bearer token in the Authorization header, not cookies.
});

builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    foreach (var role in Enum.GetValues<Role>())
    {
        var roleName = role.ToString();
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    var existingUsers = await userManager.Users.ToListAsync();
    foreach (var user in existingUsers)
    {
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Count == 0)
        {
            await userManager.AddToRoleAsync(user, nameof(Role.Employee));
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Employee Management API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseMiddleware<ApiLoggingMiddleware>();

app.UseCors("NextJsClient");

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapEmployeeEndpoints();
app.MapStatisticsEndpoints();
app.MapUserEndpoints();

app.Run();
