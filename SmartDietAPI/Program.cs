using Microsoft.EntityFrameworkCore;
using SmartDietAPI.Models;
using SmartDietAPI.Repository;
using System;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// EF Core
builder.Services.AddDbContext<AppDbContext>(opt =>opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Session cookie (simple login)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".NutriFit.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // because SameSite=None requires Secure
    options.IdleTimeout = TimeSpan.FromHours(8);
});

// CORS (for static HTML/jQuery running on Live Server)
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("web", p =>
        p.WithOrigins("http://localhost:5500", "http://127.0.0.1:5500")
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

// Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<PlanService>();
builder.Services.AddScoped<ReportService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseCors("web");
app.UseSession();

app.MapControllers();
app.Run();