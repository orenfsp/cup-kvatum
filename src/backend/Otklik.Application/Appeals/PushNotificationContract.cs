using System.Text.Json;

namespace Otklik.Application.Appeals;

public static class PushNotificationContract
{
    public const string Title = "Отклик";
    public const string Body = "В обращении есть обновление";
    public const string RelativeUrl = "/appeal/status";

    public static string PayloadJson { get; } = JsonSerializer.Serialize(new
    {
        title = Title,
        body = Body,
        url = RelativeUrl
    });
}
