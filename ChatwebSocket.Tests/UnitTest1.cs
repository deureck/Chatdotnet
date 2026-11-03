using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ChatwebSocket.Tests;

public class UnitTest1
{
    private static ServiceProvider CreateTestServiceProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddDbContext<DbContex>(options => options.UseSqlite(connection));
        services.AddScoped<ServiceCreaterUser>();
        services.AddScoped<ServicesMessager>();
        services.AddSingleton<ChatWebSocketManager>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContex>();
        db.Database.EnsureCreated();
        return provider;
    }

    [Fact]
    public async Task UserAndMessageServices_ShouldPersistInSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = CreateTestServiceProvider(connection);

        using var scope = provider.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<ServiceCreaterUser>();
        var messageService = scope.ServiceProvider.GetRequiredService<ServicesMessager>();

        await userService.CreateUserInDatabase("Alice", "alice@example.com");
        var users = await userService.GetAllUsers();
        Assert.Single(users);
        Assert.Equal("Alice", users[0].Nickname);

        var savedMsg = await messageService.SaveMessageInDatabase(users[0].Uid, "Olá, mundo!");
        var allMessages = await messageService.GetAllMessages();
        Assert.Single(allMessages);
        Assert.Equal("Olá, mundo!", allMessages[0].Body);
        Assert.Equal(users[0].Uid, savedMsg.UserId);
    }

    [Fact]
    public async Task WebSocketServer_ShouldBroadcastMessagesBetweenClients()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new UniqueIdJsonConverter()));
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(ChatController).Assembly)
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new UniqueIdJsonConverter()));
        builder.Services.AddDbContext<DbContex>(options => options.UseSqlite(connection));
        builder.Services.AddScoped<ServiceCreaterUser>();
        builder.Services.AddScoped<ServicesMessager>();
        builder.Services.AddSingleton<ChatWebSocketManager>();

        await using var app = builder.Build();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbContex>();
            await db.Database.EnsureCreatedAsync();
        }

        app.UseWebSockets();
        app.MapControllers();
        await app.StartAsync();

        var address = app.Urls.First();
        var wsUri = address.Replace("http://", "ws://") + "/ws?nickname=Bob";

        using var clientWs = new ClientWebSocket();
        await clientWs.ConnectAsync(new Uri(wsUri), CancellationToken.None);

        // 1. Read join system message
        var joinMsg = await ReceiveTextAsync(clientWs);
        Assert.Contains("Bob entrou no chat", joinMsg);

        // 2. Send a chat message over WebSocket
        var sendBytes = Encoding.UTF8.GetBytes("Mensagem via WebSocket!");
        await clientWs.SendAsync(sendBytes, WebSocketMessageType.Text, true, CancellationToken.None);

        // 3. Receive broadcast of the chat message
        var chatBroadcast = await ReceiveTextAsync(clientWs);
        using var doc = JsonDocument.Parse(chatBroadcast);
        Assert.Equal("message", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("Bob", doc.RootElement.GetProperty("nickname").GetString());
        Assert.Equal("Mensagem via WebSocket!", doc.RootElement.GetProperty("body").GetString());

        await clientWs.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
        await app.StopAsync();
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket ws)
    {
        var buffer = new byte[4096];
        var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
}
