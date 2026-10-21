using Microsoft.Extensions.DependencyInjection;
using MSLX.SDK.IServices;

namespace MSLX.Plugin.QqWhitelist.Services;

/// <summary>
/// Minecraft 控制台指令执行服务
/// </summary>
public class WhitelistCommandExecutor
{
    private readonly IServiceProvider _serviceProvider;

    public WhitelistCommandExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private IInstanceConsoleService? GetConsoleService()
    {
        return _serviceProvider.GetService<IInstanceConsoleService>();
    }

    /// <summary>
    /// 向运行中的实例下发添加白名单指令
    /// </summary>
    public bool ExecuteAddCommand(uint serverId, string template, string username, string uuid)
    {
        var consoleService = GetConsoleService();
        if (consoleService == null)
        {
            SDK.MSLX.Logger.Warn("[QqWhitelist] 未能获取 IInstanceConsoleService 服务实例");
            return false;
        }

        var cmdTemplate = string.IsNullOrWhiteSpace(template) ? "whitelist add {username}" : template;
        var rendered = cmdTemplate
            .Replace("{username}", username)
            .Replace("{uuid}", uuid);

        var lines = rendered.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var allSuccess = true;

        foreach (var line in lines)
        {
            var cmd = line.Trim();
            if (string.IsNullOrWhiteSpace(cmd)) continue;

            var success = consoleService.SendCommand(serverId, cmd);
            if (!success)
            {
                allSuccess = false;
                SDK.MSLX.Logger.Warn($"[QqWhitelist] 向实例 {serverId} 下发指令 '{cmd}' 失败");
            }
        }

        // 默认若使用的是官方 whitelist 指令，补发一条 reload 确保内存完全同步
        if (cmdTemplate.Contains("whitelist add", StringComparison.OrdinalIgnoreCase))
        {
            consoleService.SendCommand(serverId, "whitelist reload");
        }

        return allSuccess;
    }

    /// <summary>
    /// 向运行中的实例下发移除白名单指令
    /// </summary>
    public bool ExecuteRemoveCommand(uint serverId, string username)
    {
        var consoleService = GetConsoleService();
        if (consoleService == null) return false;

        var success = consoleService.SendCommand(serverId, $"whitelist remove {username}");
        consoleService.SendCommand(serverId, "whitelist reload");
        return success;
    }
}
