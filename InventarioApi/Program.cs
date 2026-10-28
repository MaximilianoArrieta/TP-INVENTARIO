using System.Text;
using System.Text.Json.Serialization;
using InventarioApi.Api.Middleware;
using InventarioApi.Api.Services;
using InventarioApi.Infrastructure.Auth;
using InventarioApi.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Usuario actual (para la auditoría) + DbContext
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Servicios propios
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ReportesService>();
builder.Services.AddScoped<CorreoService>();
builder.Services.AddScoped<VentaService>();
builder.Services.AddHttpClient();

// QuestPDF (reportes): licencia gratuita para uso académico / organizaciones chicas
QuestPDF.Settings.License = LicenseType.Community;

// Controladores + Swagger
builder.Services.AddControllers()
    // Red de seguridad: si algún endpoint devuelve entidades con relaciones cruzadas, no explota
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventario API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresar solo el token (sin escribir la palabra 'Bearer')"
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});

// CORS permisivo
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// JWT
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Datos iniciales: cliente "Consumidor Final" y primer administrador
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        await DatosIniciales.CargarAsync(db, app.Configuration);

        // Datos de demostración (solo si "DatosDemo:Cargar" es true y la base no tiene productos)
        if (app.Configuration.GetValue<bool>("DatosDemo:Cargar"))
        {
            var diferencia = app.Configuration.GetValue("Reportes:DiferenciaHorariaHoras", -3);
            if (await DatosDemo.CargarAsync(db, diferencia))
                app.Logger.LogInformation("Datos de demostración cargados.");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "No se pudieron cargar los datos iniciales (¿faltan aplicar migraciones?).");
    }
}

// Primero de todo: cualquier error que ocurra más adelante queda registrado
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("PermitirTodo");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
