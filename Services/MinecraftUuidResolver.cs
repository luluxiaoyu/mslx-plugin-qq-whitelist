using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace MSLX.Plugin.QqWhitelist.Services;

/// <summary>
/// Minecraft UUID 解析服务
/// 提供标准 Java 离线 UUIDv3 算法、Mojang 官方正版 API 解析与 usercache.json 匹配
/// </summary>
public class MinecraftUuidResolver
{
    private static readonly Regex CleanUuidRegex = new("^[0-9a-fA-F]{32}$", RegexOptions.Compiled);

    /// <summary>
    /// 标准 Java Version 3 (MD5 命名空间) 离线玩家 UUID 算法
    /// 与 Java 原生 UUID.nameUUIDFromBytes(("OfflinePlayer:" + name).getBytes(StandardCharsets.UTF_8)) 100% 一致
    /// </summary>
    public static string GetOfflinePlayerUuid(string username)
    {
        var rawBytes = Encoding.UTF8.GetBytes("OfflinePlayer:" + username);
        var hash = MD5.HashData(rawBytes);

        // 设置 Version 3 (hash[6] 高4位为 0011 -> 0x30)
        hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
        // 设置 RFC 4122 Variant (hash[8] 高2位为 10 -> 0x80)
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"{hex[..8]}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex[20..]}";
    }

    /// <summary>
    /// 将 32 位无横杠 UUID 字符串转换为标准 8-4-4-4-12 格式
    /// </summary>
    public static string FormatUuid(string raw)
    {
        var clean = raw.Replace("-", "").Trim().ToLowerInvariant();
        if (clean.Length == 32 && CleanUuidRegex.IsMatch(clean))
        {
            return $"{clean[..8]}-{clean.Substring(8, 4)}-{clean.Substring(12, 4)}-{clean.Substring(16, 4)}-{clean[20..]}";
        }
        return raw;
    }

    /// <summary>
    /// 从 Mojang 官方 API 或镜像获取正版玩家 UUID
    /// </summary>
    public async Task<string?> GetOnlinePlayerUuidAsync(string username)
    {
        try
        {
            // 1. 尝试 Mojang 官方 API
            var mojangRes = await SDK.MSLX.Http.GetAsync($"https://api.mojang.com/users/profiles/minecraft/{username}");
            if (mojangRes.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(mojangRes.Content))
            {
                var obj = JObject.Parse(mojangRes.Content);
                var rawId = obj["id"]?.ToString();
                if (!string.IsNullOrWhiteSpace(rawId))
                {
                    return FormatUuid(rawId);
                }
            }
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Warn($"[QqWhitelist] 官方 Mojang API 查询 '{username}' 失败: {ex.Message}，尝试使用镜像源重试...");
        }

        try
        {
            // 2. 备选：PlayerDB 开放 API
            var playerDbRes = await SDK.MSLX.Http.GetAsync($"https://playerdb.co/api/player/minecraft/{username}");
            if (playerDbRes.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(playerDbRes.Content))
            {
                var obj = JObject.Parse(playerDbRes.Content);
                var rawId = obj["data"]?["player"]?["id"]?.ToString();
                if (!string.IsNullOrWhiteSpace(rawId))
                {
                    return FormatUuid(rawId);
                }
            }
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Warn($"[QqWhitelist] PlayerDB API 查询 '{username}' 失败: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 读取 server.properties 中的 online-mode 配置
    /// </summary>
    public bool IsServerOnlineMode(string serverBasePath)
    {
        try
        {
            var propPath = Path.Combine(serverBasePath, "server.properties");
            if (!File.Exists(propPath)) return false;

            foreach (var line in File.ReadAllLines(propPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('#') || trimmed.StartsWith('!')) continue;

                var idx = trimmed.IndexOf('=');
                if (idx <= 0) continue;

                var key = trimmed[..idx].Trim();
                var val = trimmed[(idx + 1)..].Trim();

                if (string.Equals(key, "online-mode", StringComparison.OrdinalIgnoreCase))
                {
                    return bool.TryParse(val, out var res) && res;
                }
            }
        }
        catch
        {
            // ignore
        }

        return false;
    }

    /// <summary>
    /// 从实例目录的 usercache.json 中检索已知 UUID
    /// </summary>
    public string? TryGetUuidFromUserCache(string serverBasePath, string username)
    {
        try
        {
            var cachePath = Path.Combine(serverBasePath, "usercache.json");
            if (!File.Exists(cachePath)) return null;

            var json = File.ReadAllText(cachePath);
            if (string.IsNullOrWhiteSpace(json)) return null;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("name", out var nameProp) &&
                        string.Equals(nameProp.GetString(), username, StringComparison.OrdinalIgnoreCase))
                    {
                        if (el.TryGetProperty("uuid", out var uuidProp))
                        {
                            var uuid = uuidProp.GetString();
                            if (!string.IsNullOrWhiteSpace(uuid)) return FormatUuid(uuid);
                        }
                    }
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    /// <summary>
    /// 综合解析玩家 UUID：
    /// 1. 优先查 usercache.json；
    /// 2. 若是正版服，尝试联网查正版 UUID；若查不到则降级离线；
    /// 3. 若是离线服，直接通过标准离线算法秒算。
    /// </summary>
    public async Task<string> ResolvePlayerUuidAsync(string serverBasePath, string username)
    {
        // 1. 优先查历史缓存
        var cachedUuid = TryGetUuidFromUserCache(serverBasePath, username);
        if (!string.IsNullOrWhiteSpace(cachedUuid)) return cachedUuid;

        // 2. 读取 online-mode
        var isOnline = IsServerOnlineMode(serverBasePath);
        if (isOnline)
        {
            var onlineUuid = await GetOnlinePlayerUuidAsync(username);
            if (!string.IsNullOrWhiteSpace(onlineUuid)) return onlineUuid;
        }

        // 3. 离线 UUID 秒算
        return GetOfflinePlayerUuid(username);
    }
}
