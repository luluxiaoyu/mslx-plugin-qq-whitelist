using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using MSLX.Plugin.QqWhitelist.Models;
using MSLX.SDK;

namespace MSLX.Plugin.QqWhitelist.Services;

/// <summary>
/// 插件配置持久化管理服务
/// 负责全局 SMTP 配置、实例独立配置及白名单审计记录的存储与检索
/// </summary>
public class QqWhitelistConfigService
{
    private static QqWhitelistConfigService? _instance;
    public static QqWhitelistConfigService Instance => _instance ??= new QqWhitelistConfigService();

    private readonly object _lock = new();
    private static readonly Regex SlugRegex = new("^[a-zA-Z0-9_-]{1,32}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static MSLX.SDK.Interfaces.IPluginConfigBridge GetBridge()
    {
        return SDK.MSLX.Config.GetPluginConfig("mslx-plugin-qq-whitelist");
    }

    private JObject GetRootConfig()
    {
        try
        {
            var bridge = GetBridge();
            return bridge?.ReadConfig() ?? new JObject();
        }
        catch
        {
            return new JObject();
        }
    }

    private void SaveRootConfig(JObject root)
    {
        var bridge = GetBridge();
        bridge?.WriteConfig(root);
    }

    #region 全局 SMTP 配置

    public SmtpConfig GetSmtpConfig()
    {
        lock (_lock)
        {
            try
            {
                var root = GetRootConfig();
                if (root.TryGetValue("Smtp", StringComparison.OrdinalIgnoreCase, out var token) && token is JObject obj)
                {
                    return obj.ToObject<SmtpConfig>() ?? new SmtpConfig();
                }
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Error($"[QqWhitelist] 读取 SMTP 配置失败: {ex.Message}");
            }

            return new SmtpConfig();
        }
    }

    public void SaveSmtpConfig(SmtpConfigDto dto)
    {
        lock (_lock)
        {
            var root = GetRootConfig();
            var current = GetSmtpConfig();

            current.Host = dto.Host.Trim();
            current.Port = dto.Port;
            current.EnableSsl = dto.EnableSsl;
            current.FromEmail = dto.FromEmail.Trim();
            current.FromName = string.IsNullOrWhiteSpace(dto.FromName) ? "MSLX 白名单助手" : dto.FromName.Trim();
            current.Username = string.IsNullOrWhiteSpace(dto.Username) ? dto.FromEmail.Trim() : dto.Username.Trim();

            // 若传入了新密码则更新，否则保持原密码
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                current.Password = dto.Password.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.SubjectTemplate))
            {
                current.SubjectTemplate = dto.SubjectTemplate.Trim();
            }

            current.CustomHtmlTemplate = dto.CustomHtmlTemplate?.Trim() ?? "";

            root["Smtp"] = JObject.FromObject(current);
            SaveRootConfig(root);
        }
    }

    public bool IsSmtpConfigured()
    {
        var smtp = GetSmtpConfig();
        return !string.IsNullOrWhiteSpace(smtp.Host) &&
               !string.IsNullOrWhiteSpace(smtp.FromEmail) &&
               !string.IsNullOrWhiteSpace(smtp.Password);
    }

    #endregion

    #region 实例独立白名单配置

    private JObject GetInstancesContainer(JObject root)
    {
        if (root.TryGetValue("Instances", StringComparison.OrdinalIgnoreCase, out var token) && token is JObject container)
        {
            return container;
        }

        var newContainer = new JObject();
        root["Instances"] = newContainer;
        return newContainer;
    }

    public InstanceWhitelistConfig GetInstanceConfig(uint serverId)
    {
        lock (_lock)
        {
            var server = SDK.MSLX.Config.Servers.GetServer(serverId);
            var defaultTitle = server?.Name ?? $"实例 #{serverId}";
            var defaultSlug = $"server-{serverId}";

            try
            {
                var root = GetRootConfig();
                var container = GetInstancesContainer(root);

                if (container.TryGetValue(serverId.ToString(), out var itemToken) && itemToken is JObject itemObj)
                {
                    var config = itemObj.ToObject<InstanceWhitelistConfig>() ?? new InstanceWhitelistConfig();
                    config.ServerId = serverId;
                    if (string.IsNullOrWhiteSpace(config.Slug)) config.Slug = defaultSlug;
                    if (string.IsNullOrWhiteSpace(config.Title)) config.Title = defaultTitle;
                    return config;
                }
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Error($"[QqWhitelist] 读取实例 {serverId} 白名单配置失败: {ex.Message}");
            }

            return new InstanceWhitelistConfig
            {
                ServerId = serverId,
                Enabled = false,
                Slug = defaultSlug,
                Title = defaultTitle,
                ExecutionMode = WhitelistExecutionMode.DirectFile,
                CommandTemplate = "whitelist add {username}",
                MaxAccountsPerQq = 1,
                AllowedNameRegex = @"^[a-zA-Z0-9_]{3,16}$",
                CodeExpireMinutes = 5,
                CodeCooldownSeconds = 60
            };
        }
    }

    public void SaveInstanceConfig(uint serverId, InstanceWhitelistConfigDto dto)
    {
        lock (_lock)
        {
            var rawSlug = string.IsNullOrWhiteSpace(dto.Slug) ? $"server-{serverId}" : dto.Slug.Trim();
            if (!SlugRegex.IsMatch(rawSlug))
            {
                throw new ArgumentException("访问路径别名仅支持 1-32 位的字母、数字、下划线及短横线");
            }

            // 检查 Slug 唯一性
            var existing = GetInstanceConfigBySlug(rawSlug);
            if (existing != null && existing.ServerId != serverId)
            {
                throw new ArgumentException($"路径别名 '{rawSlug}' 已被其他实例占用，请使用其他别名");
            }

            var root = GetRootConfig();
            var container = GetInstancesContainer(root);

            var current = GetInstanceConfig(serverId);
            current.Enabled = dto.Enabled;
            current.Slug = rawSlug;
            current.Title = dto.Title.Trim();
            current.Notice = dto.Notice.Trim();
            current.ExecutionMode = (WhitelistExecutionMode)dto.ExecutionMode;
            current.CommandTemplate = string.IsNullOrWhiteSpace(dto.CommandTemplate) ? "whitelist add {username}" : dto.CommandTemplate.Trim();
            current.MaxAccountsPerQq = Math.Max(1, dto.MaxAccountsPerQq);
            current.AllowedNameRegex = string.IsNullOrWhiteSpace(dto.AllowedNameRegex) ? @"^[a-zA-Z0-9_]{3,16}$" : dto.AllowedNameRegex.Trim();
            current.CodeExpireMinutes = Math.Clamp(dto.CodeExpireMinutes, 1, 60);
            current.CodeCooldownSeconds = Math.Clamp(dto.CodeCooldownSeconds, 10, 300);

            container[serverId.ToString()] = JObject.FromObject(current);
            SaveRootConfig(root);
        }
    }

    public InstanceWhitelistConfig? GetInstanceConfigBySlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;

        lock (_lock)
        {
            try
            {
                var root = GetRootConfig();
                var container = GetInstancesContainer(root);

                foreach (var prop in container.Properties())
                {
                    if (prop.Value is JObject obj)
                    {
                        var itemSlug = (obj["slug"] ?? obj["Slug"])?.ToString()?.Trim();
                        if (string.Equals(itemSlug, slug.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            if (uint.TryParse(prop.Name, out var serverId))
                            {
                                return GetInstanceConfig(serverId);
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
    }

    public InstanceWhitelistConfig? GetInstanceConfigByIdOrSlug(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;

        var clean = identifier.Trim();
        if (uint.TryParse(clean, out var id))
        {
            var cfg = GetInstanceConfig(id);
            if (cfg.Enabled) return cfg;
        }

        return GetInstanceConfigBySlug(clean);
    }

    #endregion

    #region 白名单审计记录持久化 (独立 records.json)

    private string GetRecordsFilePath()
    {
        var dataDir = GetBridge()?.GetDataPath() ?? "data";
        var basePath = Path.GetFullPath(dataDir);
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

        var fullPath = Path.GetFullPath(Path.Combine(basePath, "records.json"));
        if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new System.Security.SecurityException("路径非法越界，禁止访问沙箱外部文件");
        }

        return fullPath;
    }

    public List<WhitelistRecord> GetAllRecords()
    {
        lock (_lock)
        {
            try
            {
                var path = GetRecordsFilePath();
                if (!File.Exists(path)) return new List<WhitelistRecord>();

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return new List<WhitelistRecord>();

                return JsonSerializer.Deserialize<List<WhitelistRecord>>(json, JsonOptions) 
                       ?? new List<WhitelistRecord>();
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Error($"[QqWhitelist] 读取 records.json 失败: {ex.Message}");
                return new List<WhitelistRecord>();
            }
        }
    }

    private void SaveAllRecords(List<WhitelistRecord> records)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(records, JsonOptions);
            File.WriteAllText(GetRecordsFilePath(), json);
        }
    }

    public List<WhitelistRecord> GetRecordsByServerId(uint serverId)
    {
        return GetAllRecords().Where(x => x.ServerId == serverId).OrderByDescending(x => x.CreatedAt).ToList();
    }

    public void AddRecord(WhitelistRecord record)
    {
        lock (_lock)
        {
            var list = GetAllRecords();
            list.Add(record);
            SaveAllRecords(list);
        }
    }

    public bool RemoveRecord(uint serverId, string recordId)
    {
        lock (_lock)
        {
            var list = GetAllRecords();
            var target = list.FirstOrDefault(x => x.ServerId == serverId && x.Id == recordId);
            if (target != null)
            {
                list.Remove(target);
                SaveAllRecords(list);
                return true;
            }
            return false;
        }
    }

    public void UpdateRecordsStatus(List<WhitelistRecord> updated)
    {
        lock (_lock)
        {
            var list = GetAllRecords();
            var map = updated.ToDictionary(x => x.Id, x => x.Status);

            foreach (var item in list)
            {
                if (map.TryGetValue(item.Id, out var newStatus))
                {
                    item.Status = newStatus;
                }
            }

            SaveAllRecords(list);
        }
    }

    public int GetBoundAccountsCount(uint serverId, string qq)
    {
        var cleanQq = qq.Trim();
        return GetAllRecords().Count(x => 
            x.ServerId == serverId && 
            x.Status != "Revoked" && 
            string.Equals(x.Qq.Trim(), cleanQq, StringComparison.OrdinalIgnoreCase)
        );
    }

    public bool IsPlayerAlreadyBound(uint serverId, string playerName)
    {
        var clean = playerName.Trim();
        return GetAllRecords().Any(x =>
            x.ServerId == serverId &&
            x.Status != "Revoked" &&
            string.Equals(x.PlayerName.Trim(), clean, StringComparison.OrdinalIgnoreCase)
        );
    }

    #endregion
}
