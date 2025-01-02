using Microsoft.EntityFrameworkCore;
using UserService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;

using Microsoft.AspNetCore.Mvc;
using Prometheus;
using UserService.AsyncDataServices;
using UserService.SyncDataServices.Grpc;
using UserService.SyncDataServices.Http;

[assembly: ApiController]

var builder = WebApplication.CreateBuilder(args);
// builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    Console.WriteLine($"Keycloak {builder.Configuration.GetSection("Keycloak")}");
    
    var keycloakConfig = builder.Configuration.GetSection("Keycloak");
    options.Authority = keycloakConfig["Authority"]; // Keycloak realm URL
    options.Audience = keycloakConfig["Audience"];   // Client ID
    options.RequireHttpsMetadata = false;            // Disable for development
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidIssuer = keycloakConfig["Authority"],
        ValidAudience = keycloakConfig["Audience"]
    }; 
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("Token validated successfully");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine("Token challenge triggered");
            return Task.CompletedTask;
        }
    };
    
    Console.WriteLine($"Authority {keycloakConfig["Authority"]}");
    Console.WriteLine($"Authority {keycloakConfig["Audience"]}");
});

builder.Services.AddAuthorization();


if (builder.Environment.IsProduction())
{
    var connectionString =  builder.Configuration.GetConnectionString("UserConn");;
    var dbPassword = builder.Configuration["ConnectionStrings:UserConn:Password"];
    
    Console.WriteLine("--> Setting SQL connection string");
    builder.Configuration["ConnectionStrings:UserConn"] = $"{connectionString};Password={dbPassword}";
    
    Console.WriteLine("---> Using SqlServer database");
    builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("UserConn")));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgressConn")));
    // Console.WriteLine("---> Using InMemory database");
    // builder.Services.AddDbContext<AppDbContext>(opt => 
    //     opt.UseInMemoryDatabase("InMem")); 
}

builder.Services.AddScoped<IUserRepo, UserRepo>();
builder.Services.AddHttpClient<IHobbyDataClient, HttpHobbyDataClient>();
builder.Services.AddSingleton<IMessageBusClient, MessageBusClient>();
builder.Services.AddGrpc();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

Console.WriteLine($"--> HobbyService Endpoint {builder.Configuration["HobbyService"]}");

// *** Add Controllers ***
builder.Services.AddControllers();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseRouting();

// Map the /metrics endpoint directly
app.MapMetrics(); // This maps the Prometheus metrics endpoint

app.UseAuthentication();
app.UseAuthorization();

// Map controllers or other routes directly
app.MapControllers(); // If you have any API controllers

// Optional: Add other middleware or configurations
app.UseHttpMetrics(); // Enables HTTP metrics

app.MapGet("/protos/users.proto", async context =>
{
    await context.Response.WriteAsync(File.ReadAllText("Protos/users.proto"));
});

app.UseCors("AllowAllOrigins");

PrepDb.PrepPopulations(app, builder.Environment.IsProduction());
app.MapGrpcService<GrpcUserService>();
var messageBusClient = app.Services.GetRequiredService<IMessageBusClient>();
((MessageBusClient)messageBusClient).StartListening("KK.EVENT.*.HobbyHub.SUCCESS.#");
// ((MessageBusClient)messageBusClient).StartListening("KK.EVENT.*.HobbyHub.ERROR.#");

app.Run();


