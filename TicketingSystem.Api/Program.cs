using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TicketingSystem.Api.Data;
using TicketingSystem.Api.Models;
using TicketingSystem.Api.Services;
using TicketingSystem.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IPhoneNumberService, PhoneNumberService>();

builder.Services.Configure<MpesaDarajaOptions>(
    builder.Configuration.GetSection("Mpesa"));

// Do NOT set DefaultRequestHeaders here — the token request uses Basic auth
// and the STK push uses Bearer auth. Each request sets its own Authorization
// header explicitly. Shared default headers cause conflicts between the two.
builder.Services.AddHttpClient<IMpesaDarajaService, MpesaDarajaService>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<MpesaDarajaOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.Configure<FornaxSmsOptions>(
    builder.Configuration.GetSection("Fornax"));

builder.Services.AddHttpClient<IFornaxSmsService, FornaxSmsService>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<FornaxSmsOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
});


builder.Services.AddScoped<ITicketSmsTemplateService, TicketSmsTemplateService>();
builder.Services.AddScoped<ITicketSmsNotificationService, TicketSmsNotificationService>();
builder.Services.AddSingleton<ITicketSmsQueue, TicketSmsQueue>();
builder.Services.AddHostedService<TicketSmsBackgroundWorker>();

// Add WhatsApp services
builder.Services.Configure<WhatsAppOptions>(
    builder.Configuration.GetSection("WhatsApp"));

builder.Services.AddMemoryCache();

builder.Services.AddSignalR();

builder.Services.AddHttpClient<IWhatsAppSenderService, WhatsAppSenderService>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<WhatsAppOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.ApiBaseUrl);
});

builder.Services.AddSingleton<IWhatsAppMessageQueue, WhatsAppMessageQueue>();
builder.Services.AddHostedService<WhatsAppMessageBackgroundWorker>();

builder.Services.AddScoped<IWhatsAppConversationService, WhatsAppConversationService>();
builder.Services.AddScoped<IWhatsAppUserResolver, WhatsAppUserResolver>();
builder.Services.AddScoped<IWhatsAppWebhookSignatureValidator, WhatsAppWebhookSignatureValidator>();


builder.Services.Configure<GoogleAuthSettings>(
    builder.Configuration.GetSection("GoogleAuth"));

var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrEmpty(jwtSecretKey))
    throw new InvalidOperationException("JWT:SecretKey is not configured in appsettings.json");

var key = Encoding.ASCII.GetBytes(jwtSecretKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<WhatsAppLiveChatHub>("/hubs/whatsapp-live-chat");

app.Run();

