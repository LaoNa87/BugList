using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

// 加載 Ocelot 配置文件
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// 註冊必要的服務
builder.Services.AddHttpContextAccessor(); // 確保 Swagger 所需的服務已註冊

// JWT 認證配置
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
    options.MapInboundClaims = false; // 禁用映射入站聲明
});

// 註冊 Ocelot 服務
builder.Services.AddOcelot();

var app = builder.Build();

// 聚合微服務的 Swagger 文件
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/api/user/swagger/v1/swagger.json", "UserService API v1");
    c.SwaggerEndpoint("/api/bug/swagger/v1/swagger.json", "BugListMicroservice API v1");
    c.RoutePrefix = "swagger";
    c.DisplayOperationId();
    c.DisplayRequestDuration();
});

// 確保 Swagger 端點允許匿名訪問
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/swagger"), appBuilder =>
{
    appBuilder.UseAuthentication();
});

// 使用 Ocelot 中間件
await app.UseOcelot();

app.Run();