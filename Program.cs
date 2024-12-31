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
    options.Authority = "http://hobbyhub.com:8080/realms/HobbyHub/"; // Keycloak realm URL
    options.Audience = "frontend-app"; // Replace with your Keycloak client ID
    options.RequireHttpsMetadata = false; // Disable in development if using HTTP
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidIssuer = "http://hobbyhub.com:8080/realms/HobbyHub/",
        ValidAudience = "frontend-app"
    };
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
    Console.WriteLine("---> Using InMemory database");
    builder.Services.AddDbContext<AppDbContext>(opt => 
        opt.UseInMemoryDatabase("InMem")); 
}

builder.Services.AddScoped<IUserRepo, UserRepo>();
builder.Services.AddHttpClient<IHobbyDataClient, HttpHobbyDataClient>();
builder.Services.AddSingleton<IMessageBusClient, MessageBusClient>();
builder.Services.AddGrpc();
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

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

PrepDb.PrepPopulations(app, builder.Environment.IsProduction());
app.MapGrpcService<GrpcUserService>();
app.Run();


