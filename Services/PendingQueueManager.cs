using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using MSLX.Plugin.QqWhitelist.Models;

namespace MSLX.Plugin.QqWhitelist.Services;

/// <summary>
/// 模式 3 离线待处理重放队列管理器
/// 监听服务端就绪日志（Done），并在开机后自动批量重放指令
/// </summary>
public class PendingQueueManager
{
    private readonly WhitelistCommandExecutor _commandExecutor;
    private readonly ConcurrentDictionary<uint, ConcurrentQueue<WhitelistRecord>> _pendingMap = new();
    private readonly object _diskLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public PendingQueueManager(WhitelistCommandExecutor commandExecutor)
    {
        _commandExecutor = commandExecutor;
        LoadFromDisk();
    }

    private string GetStorageFilePath()
    {
        var dataDir = SDK.MSLX.Config.GetPluginConfig("mslx-plugin-qq-whitelist")?.GetDataPath() ?? "data";
        var basePath = Path.GetFullPath(dataDir);
        if (!Directory.Exists(basePath)) Directory.CreateDirectory(basePath);

        var fullPath = Path.GetFullPath(Path.Combine(basePath, "pending_queue.json"));
        if (!fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new System.Security.SecurityException("路径非法越界，禁止访问沙箱外部文件");
        }

        return fullPath;
    }

    private void LoadFromDisk()
    {
        lock (_diskLock)
        {
            try
            {
                var path = GetStorageFilePath();
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return;

                var list = JsonSerializer.Deserialize<List<WhitelistRecord>>(json, JsonOptions);
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        var queue = _pendingMap.GetOrAdd(item.ServerId, _ => new ConcurrentQueue<WhitelistRecord>());
                        queue.Enqueue(item);
                    }
                }
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Error($"[QqWhitelist] 加载待处理重放队列失败: {ex.Message}");
            }
        }
    }

    private void SaveToDisk()
    {
        lock (_diskLock)
        {
            try
            {
                var allItems = new List<WhitelistRecord>();
                foreach (var queue in _pendingMap.Values)
                {
                    allItems.AddRange(queue.ToArray());
                }

                var json = JsonSerializer.Serialize(allItems, JsonOptions);
                File.WriteAllText(GetStorageFilePath(), json);
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Error($"[QqWhitelist] 保存待处理重放队列失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 将待处理记录加入重放队列
    /// </summary>
    public void Enqueue(WhitelistRecord record)
    {
        var queue = _pendingMap.GetOrAdd(record.ServerId, _ => new ConcurrentQueue<WhitelistRecord>());
        queue.Enqueue(record);
        SaveToDisk();
    }

    /// <summary>
    /// 处理服务端日志接收事件
    /// 当检测到 Done 标志时自动重放所有待处理指令
    /// </summary>
    public void HandleServerLog(uint serverId, string logLine)
    {
        if (string.IsNullOrWhiteSpace(logLine)) return;

        // 检测 Minecraft 完全启动就绪的标准日志
        var isReady = logLine.Contains("Done (", StringComparison.OrdinalIgnoreCase) ||
                      logLine.Contains("Server started.", StringComparison.OrdinalIgnoreCase) ||
                      logLine.Contains("For help, type \"help\"", StringComparison.OrdinalIgnoreCase);

        if (!isReady) return;

        if (!_pendingMap.TryGetValue(serverId, out var queue) || queue.IsEmpty) return;

        Task.Run(async () =>
        {
            // 稍等 1 秒让命令系统完全稳定
            await Task.Delay(1000);

            SDK.MSLX.Logger.Info($"[QqWhitelist] 捕获到实例 {serverId} 启动就绪日志，开始批量重放待处理白名单指令...");

            var processedList = new List<WhitelistRecord>();
            while (queue.TryDequeue(out var record))
            {
                try
                {
                    _commandExecutor.ExecuteAddCommand(
                        record.ServerId,
                        "whitelist add {username}",
                        record.PlayerName,
                        record.PlayerUuid
                    );
                    record.Status = "Active";
                    processedList.Add(record);
                    SDK.MSLX.Logger.Info($"[QqWhitelist] 成功为玩家 '{record.PlayerName}' 自动补发白名单指令");
                }
                catch (Exception ex)
                {
                    SDK.MSLX.Logger.Error($"[QqWhitelist] 补发白名单指令异常: {ex.Message}");
                }
            }

            SaveToDisk();

            // 同步更新全局记录状态
            if (processedList.Count > 0)
            {
                QqWhitelistConfigService.Instance.UpdateRecordsStatus(processedList);
            }
        });
    }
}
