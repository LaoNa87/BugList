using Line.Messaging;
using LineBotService.Services;
using SharedModels.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// µù¥U LINE Bot «È¤áºÝ
builder.Services.AddSingleton<ILineMessagingClient>(sp =>
    new LineMessagingClient(sp.GetRequiredService<IConfiguration>()["LineBot:ChannelAccessToken"]));
builder.Services.AddHttpClient<ILineBotClient, LineBotClient>();
builder.Services.AddSingleton<IMessageQueue,RabbitMQClient>();
builder.Services.AddHostedService<LineBotReplyConsumer>();

builder.Services.AddMemoryCache();

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
