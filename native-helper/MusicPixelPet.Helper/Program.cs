using System.Text;
using System.Text.Json;

namespace MusicPixelPet.Helper;

// Helper 进程通过标准输入/输出与 Electron 主进程通信。
internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        // MediaSessionWatcher 负责监听 Windows 媒体会话，并把快照写回 stdout。
        var watcher = new MediaSessionWatcher(Console.Out);
        await watcher.StartAsync();

        while (true)
        {
            var line = await Console.In.ReadLineAsync();

            if (line is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                // 每行 stdin 都是一条 Electron 主进程发送的 JSON 请求。
                var request = JsonSerializer.Deserialize<HelperRequest>(line, JsonOptions);

                if (request is null)
                {
                    continue;
                }

                await watcher.HandleRequestAsync(request);
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync(exception.Message);
            }
        }
    }
}
