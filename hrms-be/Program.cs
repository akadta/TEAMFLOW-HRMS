using System.Text;
using System.Threading.RateLimiting;
using hrms.Data;
using hrms.Seeders;
using hrms.Shared.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<LoginThrottle>();
builder.Services.Configure<MailOptions>(builder.Configuration.GetSection("Mail"));
builder.Services.AddScoped<EmailService>();


// real client IP + original scheme when running behind Apache on the same server
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

builder.Services.AddCors(o => o.AddPolicy("web", p => p
    .WithOrigins(
        "http://localhost:4200",   // main frontend
        "http://localhost:4201",    // company frontend
        "http://62.72.12.218:9076",
        "http://62.72.12.218:9077"
)   // add your https origins when you have a domain
    .WithExposedHeaders("Retry-After")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddSingleton<JwtService>();
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<MainPermissionService>();
builder.Services.AddScoped<AttendanceRules>();
builder.Services.AddHostedService<AttendanceFinalizer>();
builder.Services.AddScoped<LeaveFlow>();
builder.Services.AddScoped<AttendanceView>();
builder.Services.AddScoped<TaskFlow>();
builder.Services.AddScoped<Notifier>();
builder.Services.AddScoped<TaskDispatcher>();
builder.Services.AddHostedService<AssignmentScheduler>();

var jwt = builder.Configuration.GetSection("Jwt");

const string CompanyScheme = "CompanyBearer";

// same validation rules for both schemes; only audience + cookie differ
void ConfigureJwt(JwtBearerOptions o, string audience, string cookieName)
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt["Issuer"],
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
        ClockSkew = TimeSpan.Zero,
        NameClaimType = "name",
        RoleClaimType = "role"
    };
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            ctx.Token = ctx.Request.Cookies[cookieName];
            return Task.CompletedTask;
        }
    };
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme,
        o => ConfigureJwt(o, jwt["Audience"]!, "access_token"))
    .AddJwtBearer(CompanyScheme,
        o => ConfigureJwt(o, jwt["CompanyAudience"]!, "company_access_token"));

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, UidProvider>();
builder.Services.AddOpenApi();
// builder.Services.AddScoped<PermissionService>();

var app = builder.Build();

if (args.Contains("seed"))
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();
    await AdminSeeder.SeedAsync(db);
    await PermissionSeeder.SeedAsync(db);
    await MainPermissionSeeder.SeedAsync(db);

    Console.WriteLine("Database seeded successfully.");

    return;
}

app.UseForwardedHeaders();
var webRoot = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(webRoot);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRoot)
});
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// no UseHttpsRedirection: Apache terminates https and proxies plain http to Kestrel
app.UseCors("web");
// app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotifHub>("/hubs/company")
    .RequireCors("web")
    .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "CompanyBearer" });
app.MapHub<NotifHub>("/hubs/main")
    .RequireCors("web")
    .RequireAuthorization();

app.Run();