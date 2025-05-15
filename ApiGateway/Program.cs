using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

// �[�� Ocelot �t�m���
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// ���U���n���A��
builder.Services.AddHttpContextAccessor(); // �T�O Swagger �һݪ��A�Ȥw���U

// JWT �{�Ұt�m
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
    options.MapInboundClaims = false; // �T�άM�g�J���n��
});

// ���U Ocelot �A��
builder.Services.AddOcelot();

var app = builder.Build();

// �E�X�L�A�Ȫ� Swagger ���
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/api/user/swagger/v1/swagger.json", "UserService API v1");
    c.SwaggerEndpoint("/api/bug/swagger/v1/swagger.json", "BugListMicroservice API v1");
    c.RoutePrefix = "swagger";
    c.DisplayOperationId();
    c.DisplayRequestDuration();
});

// �T�O Swagger ���I���\�ΦW�X��
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/swagger"), appBuilder =>
{
    appBuilder.UseAuthentication();
});

// �ϥ� Ocelot ������
await app.UseOcelot();

app.Run();