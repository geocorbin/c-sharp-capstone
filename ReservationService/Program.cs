using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using ReservationService.Data;
using ReservationService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Reservation Service API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Description = "Paste just the token - Swagger adds the \"Bearer \" prefix for you."
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<ReservationServiceContext>(options =>
        options.UseInMemoryDatabase("ReservationServiceDb"));
}
else
{
    var connectionString = builder.Configuration.GetConnectionString("ReservationDb");
    builder.Services.AddDbContext<ReservationServiceContext>(options =>
        options.UseNpgsql(connectionString));
}

var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            RoleClaimType = "role",
            NameClaimType = "userId"
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient<IUserServiceClient, UserServiceClient>(client =>
{
    var baseUrl = builder.Configuration["ServiceUrls:UserService"]
        ?? throw new InvalidOperationException("ServiceUrls:UserService is not configured.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient<ICatalogServiceClient, CatalogServiceClient>(client =>
{
    var baseUrl = builder.Configuration["ServiceUrls:CatalogService"]
        ?? throw new InvalidOperationException("ServiceUrls:CatalogService is not configured.");
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<IWaitlistCascadeService, WaitlistCascadeService>();
builder.Services.AddHostedService<WaitlistExpiryJob>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ReservationServiceContext>().Database.Migrate();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", async (ReservationServiceContext context) =>
{
    int migrationsApplied;
    try
    {
        migrationsApplied = (await context.Database.GetAppliedMigrationsAsync()).Count();
    }
    catch (NotSupportedException)
    {
        migrationsApplied = 0;
    }

    return Results.Ok(new { service = "ReservationService", status = "UP", database = "reservationservicedb", migrations = migrationsApplied });
});

app.Run();