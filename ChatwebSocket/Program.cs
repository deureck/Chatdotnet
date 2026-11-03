using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using ChatwebSocket;

var builder = WebApplication.CreateBuilder(args);

// 1. ALL service registrations must happen BEFORE builder.Build()
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new UniqueIdJsonConverter());
});
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new UniqueIdJsonConverter());
});

builder.Services.AddDbContext<DbContex>();
builder.Services.AddScoped<ServiceCreaterUser>();
builder.Services.AddScoped<ServicesMessager>();
builder.Services.AddSingleton<ChatWebSocketManager>();

// 2. Build the app ONCE after services are registered
var app = builder.Build();

Directory.CreateDirectory("./storage");
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DbContex>();
    db.Database.EnsureCreated();
}

// 3. Configure middleware / request pipeline AFTER building
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromMinutes(2)
};

app.UseWebSockets(webSocketOptions);
app.MapControllers();

app.Run();

public class UniqueIdJsonConverter : JsonConverter<UniqueId>
{
    public override UniqueId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return string.IsNullOrWhiteSpace(value) ? new UniqueId() : ChatWebSocketManager.ParseUniqueId(value);
    }

    public override void Write(Utf8JsonWriter writer, UniqueId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public partial class Program { }
