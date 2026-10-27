var builder = WebApplication.CreateBuilder(args);

// 1. ALL service registrations must happen BEFORE builder.Build()
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// 2. Build the app ONCE after services are registered
var app = builder.Build();

// 3. Configure middleware / request pipeline AFTER building
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};

app.UseWebSockets(webSocketOptions);
app.MapControllers();

app.Run();