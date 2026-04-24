using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using Dashboard.DAL;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1. Database Context
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .UseSnakeCaseNamingConvention());

// 2. Repositories & Services
builder.Services.AddScoped<Dashboard.DAL.Repositories.IFtoRepository, Dashboard.DAL.Repositories.FtoRepository>();
builder.Services.AddScoped<Dashboard.DAL.Repositories.IBillRepository, Dashboard.DAL.Repositories.BillRepository>();
builder.Services.AddScoped<Dashboard.DAL.Repositories.IDashboardRepository, Dashboard.DAL.Repositories.DashboardRepository>();
builder.Services.AddScoped<Dashboard.BLL.Services.IWorkflowService, Dashboard.BLL.Services.WorkflowService>();
builder.Services.AddScoped<Dashboard.BLL.Services.ISimulationService, Dashboard.BLL.Services.SimulationService>();
builder.Services.AddScoped<Dashboard.BLL.Services.IFiscalYearUtility, Dashboard.BLL.Services.FiscalYearUtility>();
builder.Services.AddScoped<Dashboard.BLL.Services.IDashboardService, Dashboard.BLL.Services.DashboardService>();
builder.Services.AddScoped<Dashboard.BLL.Services.IDashboardUpdateService, Dashboard.PL.Services.DashboardUpdateService>();
builder.Services.AddScoped<Dashboard.BLL.Utilities.IResourceMonitor, Dashboard.BLL.Utilities.ResourceMonitor>();

builder.Services.AddHostedService<Dashboard.PL.Workers.DashboardAggregationWorker>();

// 2. Authentication & JWT
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/signalr-hub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// 3. SignalR
builder.Services.AddSignalR()
    .AddMessagePackProtocol();

// 4. Controllers & Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 5. CORS for Angular
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4201")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("CorsPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.UseWebSockets();

app.MapControllers();
app.MapHub<Dashboard.PL.Hubs.SignalRHub>("/signalr-hub");

app.Run();
