using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSLX.Plugin.QqWhitelist.Services;

/// <summary>
/// 纯 JSON 实体模型，对齐 Minecraft 官方 whitelist.json 格式
/// </summary>
public class MinecraftWhitelistItem
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

/// <summary>
/// Minecraft whitelist.json 文件安全读写服务
/// </summary>
public class WhitelistFileWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static (bool IsSafe, string FullPath, string Message) GetSafeFilePath(string basePath)
    {
        try
        {
            var fullBasePath = Path.GetFullPath(basePath);
            var fullFilePath = Path.GetFullPath(Path.Combine(fullBasePath, "whitelist.json"));
            if (!fullFilePath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
            {
                return (false, string.Empty, "路径非法越界");
            }

            return (true, fullFilePath, "成功");
        }
        catch (Exception ex)
        {
            return (false, string.Empty, $"路径校验异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 读取白名单文件列表
    /// </summary>
    public List<MinecraftWhitelistItem> ReadWhitelist(string serverBasePath)
    {
        var (isSafe, filePath, _) = GetSafeFilePath(serverBasePath);
        if (!isSafe || !File.Exists(filePath))
        {
            return new List<MinecraftWhitelistItem>();
        }

        try
        {
            var json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json)) return new List<MinecraftWhitelistItem>();

            return JsonSerializer.Deserialize<List<MinecraftWhitelistItem>>(json, JsonOptions) 
                   ?? new List<MinecraftWhitelistItem>();
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"[QqWhitelist] 读取 whitelist.json 失败: {ex.Message}");
            return new List<MinecraftWhitelistItem>();
        }
    }

    /// <summary>
    /// 将玩家加入 whitelist.json 文件
    /// </summary>
    public (bool Success, string Message) AddPlayer(string serverBasePath, string username, string uuid)
    {
        var (isSafe, filePath, error) = GetSafeFilePath(serverBasePath);
        if (!isSafe) return (false, error);

        try
        {
            var list = ReadWhitelist(serverBasePath);
            
            // 检查是否已经存在
            if (list.Any(x => x.Name.Equals(username, StringComparison.OrdinalIgnoreCase)))
            {
                return (true, "玩家已存在于白名单文件中");
            }

            list.Add(new MinecraftWhitelistItem
            {
                Name = username,
                Uuid = uuid
            });

            var json = JsonSerializer.Serialize(list, JsonOptions);
            File.WriteAllText(filePath, json);

            return (true, "成功写入白名单文件");
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"[QqWhitelist] 写入 whitelist.json 失败: {ex.Message}");
            return (false, $"修改白名单文件失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 从 whitelist.json 文件中移除玩家
    /// </summary>
    public (bool Success, string Message) RemovePlayer(string serverBasePath, string username)
    {
        var (isSafe, filePath, error) = GetSafeFilePath(serverBasePath);
        if (!isSafe) return (false, error);

        try
        {
            var list = ReadWhitelist(serverBasePath);
            var count = list.RemoveAll(x => x.Name.Equals(username, StringComparison.OrdinalIgnoreCase));
            if (count > 0)
            {
                var json = JsonSerializer.Serialize(list, JsonOptions);
                File.WriteAllText(filePath, json);
                return (true, "已从白名单文件中移除");
            }

            return (true, "玩家原本不在白名单文件中");
        }
        catch (Exception ex)
        {
            return (false, $"更新白名单文件异常: {ex.Message}");
        }
    }
}
