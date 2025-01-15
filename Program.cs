using Microsoft.EntityFrameworkCore;
using UserService.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
//     o.CustomSchemaIds(id => id.FullName!.Replace("+", "-"));
//     o.AddSecurityDefinition("Keycloak", new OpenApiSecurityScheme
//     {
//         Type = SecuritySchemeType.OAuth2,
//         Flows = new OpenApiOAuthFlows
//         {
//             Implicit = new OpenApiOAuthFlow
//             {
//                 AuthorizationUrl = new Uri(builder.Configuration.GetConnectionString("Keycloak:AuthenticationURL")!),
//                 Scopes = new Dictionary<string, string>
//                 {
//                     {"openid", "openid"},
//                     {"profile", "profile"}
//                 }
//             }
//         }
//     });
//     var securityRequirement = new OpenApiSecurityRequirement
//     {
//         {
//             new OpenApiSecurityScheme
//             {
//                 Reference = new OpenApiReference
//                 {
//                     Id = "Keycloak",
//                     Type = ReferenceType.SecurityScheme
//                 },
//                 In = ParameterLocation.Header,
//                 Name = "Bearer",
//                 Scheme = "Bearer",
//             },
//             []
//         }
//     };
//     o.AddSecurityRequirement(securityRequirement);
});

if (builder.Environment.IsProduction())
{
    //--> This is the old code that uses the MSSQL database:
    // var connectionString =  builder.Configuration.GetConnectionString("UserConn");;
    // var dbPassword = builder.Configuration["ConnectionStrings:UserConn:Password"];
    //
    // Console.WriteLine("--> Setting SQL connection string");
    // builder.Configuration["ConnectionStrings:UserConn"] = $"{connectionString};Password={dbPassword}";
    //
    // Console.WriteLine("---> Using SqlServer database");
    // builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("UserConn")));


    //Set inside Kubernetes
    var dbUser = Environment.GetEnvironmentVariable("POSTGRES_USER");
    var dbHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
    var dbPort = Environment.GetEnvironmentVariable("POSTGRES_PORT");
    var dbPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

    if (string.IsNullOrEmpty(dbUser) || string.IsNullOrEmpty(dbHost) || string.IsNullOrEmpty(dbPort) ||
        string.IsNullOrEmpty(dbPassword))
    {
        
        Console.WriteLine("One of the string values for Postgres are empty");
        Console.WriteLine($"Host={dbHost};Port={dbPort};Database=Users;Username={dbUser};Password={dbPassword};Trust Server Certificate=true;");
        
    }
    builder.Configuration["ConnectionStrings:PostgressConn"] = $"Host={dbHost};Port={dbPort};Database=Users;Username={dbUser};Password={dbPassword};Trust Server Certificate=true;";
    

    Console.WriteLine("---> Trying to connect to database");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgressConn")));
    
    // Read Keycloak values from environment variables
    builder.Configuration["Keycloak:ClientId"] = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENTID");
    builder.Configuration["Keycloak:ClientSecret"] = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENTSECRET");
    
    builder.Configuration["Keycloak:Authority"] = Environment.GetEnvironmentVariable("KEYCLOAK_AUTHORITY");
    builder.Configuration["Keycloak:Audience"] = Environment.GetEnvironmentVariable("KEYCLOAK_AUDIENCE");
    builder.Configuration["Keycloak:AuthenticationURL"] = Environment.GetEnvironmentVariable("KEYCLOAK_AUTHENTICATION_URL");
    
    Console.WriteLine("Keycloak Configuration:");
    Console.WriteLine($"ClientId: {builder.Configuration["Keycloak:ClientId"]}");
    Console.WriteLine($"ClientSecret: {builder.Configuration["Keycloak:ClientSecret"]}"); // Be cautious with sensitive info
    Console.WriteLine($"Authority: {builder.Configuration["Keycloak:Authority"]}");
    Console.WriteLine($"Audience: {builder.Configuration["Keycloak:Audience"]}");
    Console.WriteLine($"AuthenticationURL: {builder.Configuration["Keycloak:AuthenticationURL"]}");
    
}
else
{
    Console.WriteLine("---> Using InMemory database");
    builder.Services.AddDbContext<AppDbContext>(opt => 
        opt.UseInMemoryDatabase("InMem")); 
}

var integrationMode = builder.Configuration.GetValue<bool>("IntegrationMode");
if (!integrationMode)
{
   builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    Console.WriteLine("---> Using Keycloak stuff");
    Console.WriteLine(builder.Configuration["Keycloak:Authority"]);
    Console.WriteLine(builder.Configuration["Keycloak:Audience"]);
    
    options.Authority = builder.Configuration["Keycloak:Authority"]; // Keycloak realm URL
    options.Audience = builder.Configuration["Keycloak:Audience"];   // Client ID
    options.RequireHttpsMetadata = false;            // Disable for development
    
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Keycloak:Authority"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Keycloak:Audience"],
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true
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
            Console.WriteLine($"Token challenge triggered: {context.Error}, {context.ErrorDescription}");
            return Task.CompletedTask;
        }
    };
    
}); 
}




builder.Services.AddAuthorization();

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
((MessageBusClient)messageBusClient).StartListening("KK.EVENT.*.HobbyHub.ERROR.#");

app.Run();


