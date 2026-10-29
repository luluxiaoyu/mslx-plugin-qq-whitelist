using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using MSLX.Plugin.QqWhitelist.Services;
using MSLX.SDK;
using MSLX.SDK.Events;
using MSLX.SDK.Interfaces;
using MSLX.SDK.Models.Plugins;

[assembly: ApplicationPart("MSLX.Plugin.QqWhitelist")]

namespace MSLX.Plugin.QqWhitelist;

public class MSLXPluginEntry : IPlugin
{
    public static MSLXPluginEntry Instance { get; private set; } = null!;

    public string Id => "mslx-plugin-qq-whitelist";
    public string Name => "自助QQ申请白名单";
    public string Description => "基于 QQ 邮箱所有权验证的 Minecraft 实例自助网页白名单申请插件，支持多实例独立配置与全局发信策略";
    public string Version => "1.0.2";
    public string Icon => "icon.png";
    public bool HasPublicPages => true;
    public string MinSDKVersion => "1.7.6";
    public string Developer => "xiaoyu";
    public string AuthorUrl => "https://github.com/luluxiaoyu/mslx-plugin-qq-whitelist";
    public string PluginUrl => "https://mslx-plugins.mslmc.net/plugins/mslx-plugin-qq-whitelist";
    public PluginMultiNodeSupport SupportsMultiNode => PluginMultiNodeSupport.Supported;

    public IServiceProvider ServiceProvider { get; private set; } = null!;

    public void OnRegisterServices(IServiceCollection services)
    {
        services.AddSingleton(QqWhitelistConfigService.Instance);
        services.AddSingleton<MinecraftUuidResolver>();
        services.AddSingleton<EmailSenderService>();
        services.AddSingleton<VerificationCodeManager>();
        services.AddSingleton<WhitelistFileWriter>();
        services.AddSingleton<WhitelistCommandExecutor>();
        services.AddSingleton<PendingQueueManager>();
        services.AddSingleton<WhitelistApplicationService>();
    }

    public void OnPluginInitialize(IServiceProvider serviceProvider)
    {
        Instance = this;
        ServiceProvider = serviceProvider;

        var dataDir = this.Config().GetDataPath();
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }
    }

    public void OnLoad()
    {
        SDK.MSLX.Logger.Info("[QqWhitelist] 自助QQ申请白名单插件已加载成功！");

        // 订阅服务端日志流事件，用于捕获 Done 就绪日志并自动重放待处理白名单指令
        SDK.MSLX.Events.OnServerLogReceived += HandleServerLogReceived;
    }

    public void OnUnload()
    {
        SDK.MSLX.Events.OnServerLogReceived -= HandleServerLogReceived;
        SDK.MSLX.Logger.Info("[QqWhitelist] 自助QQ申请白名单插件已安全卸载。");
    }

    public void OnRegisterEndpoints(IEndpointRouteBuilder endpoints)
    {
    }

    private void HandleServerLogReceived(object? sender, ServerLogEventArgs e)
    {
        // 遵循 Golden Rule 4：任何事件监听处理器必须异步化，绝不能阻塞宿主主执行流
        Task.Run(() =>
        {
            try
            {
                var pendingManager = ServiceProvider.GetService<PendingQueueManager>();
                pendingManager?.HandleServerLog(e.InstanceId, e.LogLine);
            }
            catch (Exception ex)
            {
                SDK.MSLX.Logger.Error($"[QqWhitelist] 处理实例 {e.InstanceId} 日志流就绪检测异常: {ex.Message}");
            }
        });
    }
}
