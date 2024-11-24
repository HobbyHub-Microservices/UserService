using Microsoft.EntityFrameworkCore;
using UserService.Data;


using Microsoft.AspNetCore.Mvc;
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

app.UseHttpsRedirection();

// *** Enable routing for Controllers ***
app.MapControllers();

app.MapGet("/protos/users.proto", async context =>
{
    await context.Response.WriteAsync(File.ReadAllText("Protos/users.proto"));
});

PrepDb.PrepPopulations(app, builder.Environment.IsProduction());
app.MapGrpcService<GrpcUserService>();
app.Run();


