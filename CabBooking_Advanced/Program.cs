using CabBooking_Advanced.Data;
using CabBooking_Advanced.Models;
using CabBooking_Advanced.Services;
using System.Diagnostics;
using StackExchange.Redis;
using Microsoft.EntityFrameworkCore;
using CabBooking_Advanced.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SQL Server
builder.Services.AddDbContext<CabDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Redis: ONE shared connection for the entire app (thread-safe, meant to be reused)
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(builder.Configuration["Redis"]!));
builder.Services.AddSingleton<DriverLocationService>();

// Scoped = a new instance per request (needed because they use DbContext)
builder.Services.AddScoped<RideServices>();
builder.Services.AddScoped<RideAssignmentService>();
builder.Services.AddScoped<DispatchService>();

// Background worker
builder.Services.AddHostedService<DispatchWorker>();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis"]!));
builder.Services.AddSingleton<DriverLocationService>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
