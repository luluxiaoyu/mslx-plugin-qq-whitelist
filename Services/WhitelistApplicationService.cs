using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using MSLX.Plugin.QqWhitelist.Models;
using MSLX.SDK.IServices;

namespace MSLX.Plugin.QqWhitelist.Services;

/// <summary>
/// 白名单申请全流程业务协同服务
/// 负责校验、发信控制、按 3 种模式调度执行白名单以及审计入库
/// </summary>
public class WhitelistApplicationService
{
    private readonly QqWhitelistConfigService _configService;
    private readonly VerificationCodeManager _codeManager;
    private readonly EmailSenderService _emailService;
    private readonly MinecraftUuidResolver _uuidResolver;
    private readonly WhitelistFileWriter _fileWriter;
    private readonly WhitelistCommandExecutor _commandExecutor;
    private readonly PendingQueueManager _pendingQueue;
    private readonly IServiceProvider _serviceProvider;

    // QQ 号码验证：首位 1-9，长度 5 到 10 位纯数字
    private static readonly Regex QqRegex = new(@"^[1-9]\d{4,9}$", RegexOptions.Compiled);

    public WhitelistApplicationService(
        QqWhitelistConfigService configService,
        VerificationCodeManager codeManager,
        EmailSenderService emailService,
        MinecraftUuidResolver uuidResolver,
        WhitelistFileWriter fileWriter,
        WhitelistCommandExecutor commandExecutor,
        PendingQueueManager pendingQueue,
        IServiceProvider serviceProvider)
    {
        _configService = configService;
        _codeManager = codeManager;
        _emailService = emailService;
        _uuidResolver = uuidResolver;
        _fileWriter = fileWriter;
        _commandExecutor = commandExecutor;
        _pendingQueue = pendingQueue;
        _serviceProvider = serviceProvider;
    }

    private IInstanceLifecycleService? GetLifecycleService()
    {
        return _serviceProvider.GetService<IInstanceLifecycleService>();
    }

    public bool IsServerOnline(uint serverId)
    {
        var lifecycle = GetLifecycleService();
        return lifecycle?.IsServerRunning(serverId) ?? false;
    }

    /// <summary>
    /// 获取公开申请页面信息
    /// </summary>
    public PublicServerInfoDto? GetPublicInfo(string? slug, uint? serverId)
    {
        InstanceWhitelistConfig? config = null;
        if (!string.IsNullOrWhiteSpace(slug))
        {
            config = _configService.GetInstanceConfigBySlug(slug.Trim());
        }
        else if (serverId.HasValue && serverId.Value > 0)
        {
            config = _configService.GetInstanceConfig(serverId.Value);
        }

        if (config == null || !config.Enabled) return null;

        var server = SDK.MSLX.Config.Servers.GetServer(config.ServerId);
        var title = string.IsNullOrWhiteSpace(config.Title) 
            ? (server?.Name ?? $"实例 #{config.ServerId}") 
            : config.Title;

        var isOnline = IsServerOnline(config.ServerId);

        // 安全探测服务器真实图标 (server-icon.png)
        var hasIcon = false;
        string? iconUrl = null;
        if (server != null && !string.IsNullOrWhiteSpace(server.Base))
        {
            try
            {
                var baseDir = Path.GetFullPath(server.Base);
                var iconPath = Path.GetFullPath(Path.Combine(baseDir, "server-icon.png"));
                if (iconPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase) && File.Exists(iconPath))
                {
                    hasIcon = true;
                    iconUrl = $"/api/plugin/mslx-plugin-qq-whitelist/whitelist/public/server-icon.png?slug={Uri.EscapeDataString(config.Slug)}";
                }
            }
            catch
            {
                // 忽略非受控路径探测异常
            }
        }

        return new PublicServerInfoDto
        {
            ServerId = config.ServerId,
            Title = title,
            Slug = config.Slug,
            Notice = config.Notice,
            Enabled = config.Enabled,
            IsServerOnline = isOnline,
            ExecutionMode = (int)config.ExecutionMode,
            MaxAccountsPerQq = config.MaxAccountsPerQq,
            AllowedNameRegex = config.AllowedNameRegex,
            HasServerIcon = hasIcon,
            ServerIconUrl = iconUrl
        };
    }

    /// <summary>
    /// 请求发送 QQ 邮箱验证码
    /// </summary>
    public async Task<(bool Success, string Message)> SendVerificationCodeAsync(
        string? slug,
        uint? serverId,
        string qq,
        string clientIp)
    {
        var cleanQq = qq?.Trim() ?? "";
        if (!QqRegex.IsMatch(cleanQq))
        {
            return (false, "QQ 号码格式不符合规范，请输入 5-10 位有效数字（首位非 0）");
        }

        var publicInfo = GetPublicInfo(slug, serverId);
        if (publicInfo == null || !publicInfo.Enabled)
        {
            return (false, "该服务器未开放白名单自助申请通道");
        }

        var config = _configService.GetInstanceConfig(publicInfo.ServerId);

        // 检查模式 2（仅在线时开放申请）
        if (config.ExecutionMode == WhitelistExecutionMode.OnlineOnly && !IsServerOnline(config.ServerId))
        {
            return (false, "服务器当前处于离线/维护状态，仅在服务器在线时开放申请");
        }

        // 检查该 QQ 绑定限额
        var boundCount = _configService.GetBoundAccountsCount(config.ServerId, cleanQq);
        if (boundCount >= config.MaxAccountsPerQq)
        {
            return (false, $"该 QQ 绑定的游戏角色已达到上限（最多 {config.MaxAccountsPerQq} 个）");
        }

        var smtp = _configService.GetSmtpConfig();
        if (string.IsNullOrWhiteSpace(smtp.FromEmail) || string.IsNullOrWhiteSpace(smtp.Password))
        {
            return (false, "系统发件配置未就绪，请联系管理员配置 SMTP 发信策略");
        }

        // 生成验证码
        var (codeOk, code, codeMsg) = _codeManager.GenerateCode(
            config.ServerId,
            cleanQq,
            clientIp,
            config.CodeExpireMinutes,
            config.CodeCooldownSeconds
        );

        if (!codeOk)
        {
            return (false, codeMsg);
        }

        // 发送邮件
        var targetEmail = $"{cleanQq}@qq.com";
        var sendResult = await _emailService.SendVerificationCodeAsync(
            smtp,
            targetEmail,
            publicInfo.Title,
            code,
            config.CodeExpireMinutes
        );

        return sendResult;
    }

    /// <summary>
    /// 提交白名单申请并执行添加
    /// </summary>
    public async Task<ApplyWhitelistResult> ApplyWhitelistAsync(
        string? slug,
        uint? serverId,
        string qq,
        string code,
        string username,
        string clientIp)
    {
        var cleanQq = qq?.Trim() ?? "";
        var cleanName = username?.Trim() ?? "";
        var cleanCode = code?.Trim() ?? "";

        if (!QqRegex.IsMatch(cleanQq))
        {
            return new ApplyWhitelistResult { Success = false, Message = "QQ 号码格式无效，请输入 5-10 位纯数字（首位非 0）" };
        }

        if (string.IsNullOrWhiteSpace(cleanCode))
        {
            return new ApplyWhitelistResult { Success = false, Message = "请输入 6 位验证码" };
        }

        var publicInfo = GetPublicInfo(slug, serverId);
        if (publicInfo == null || !publicInfo.Enabled)
        {
            return new ApplyWhitelistResult { Success = false, Message = "该服务器未开放白名单申请通道" };
        }

        var config = _configService.GetInstanceConfig(publicInfo.ServerId);
        var server = SDK.MSLX.Config.Servers.GetServer(config.ServerId);
        if (server == null)
        {
            return new ApplyWhitelistResult { Success = false, Message = "目标服务器实例不存在" };
        }

        // 游戏名正则校验
        var nameRegex = new Regex(config.AllowedNameRegex);
        if (!nameRegex.IsMatch(cleanName))
        {
            return new ApplyWhitelistResult { Success = false, Message = "游戏名不符合格式要求，请输入合法的游戏 ID" };
        }

        // 检查模式 2（仅在线时开放申请）
        var isOnline = IsServerOnline(config.ServerId);
        if (config.ExecutionMode == WhitelistExecutionMode.OnlineOnly && !isOnline)
        {
            return new ApplyWhitelistResult { Success = false, Message = "服务器当前处于离线/维护状态，仅在服务器在线时开放申请" };
        }

        // 检查是否已被绑定
        if (_configService.IsPlayerAlreadyBound(config.ServerId, cleanName))
        {
            return new ApplyWhitelistResult { Success = false, Message = $"游戏角色 '{cleanName}' 已在白名单中，无需重复申请" };
        }

        // 检查 QQ 绑定配额
        var boundCount = _configService.GetBoundAccountsCount(config.ServerId, cleanQq);
        if (boundCount >= config.MaxAccountsPerQq)
        {
            return new ApplyWhitelistResult { Success = false, Message = $"该 QQ 绑定的游戏角色已达上限（最多 {config.MaxAccountsPerQq} 个）" };
        }

        // 校验验证码
        var (codeValid, verifyMsg) = _codeManager.VerifyCode(config.ServerId, cleanQq, cleanCode);
        if (!codeValid)
        {
            return new ApplyWhitelistResult { Success = false, Message = verifyMsg };
        }

        // 解析 UUID
        var uuid = await _uuidResolver.ResolvePlayerUuidAsync(server.Base, cleanName);

        var finalStatus = "Active";

        // 按 3 种模式调度执行
        switch (config.ExecutionMode)
        {
            case WhitelistExecutionMode.DirectFile: // 模式 1：在线发指令并落盘，离线精准改写 whitelist.json
                if (isOnline)
                {
                    _commandExecutor.ExecuteAddCommand(config.ServerId, config.CommandTemplate, cleanName, uuid);
                }
                else
                {
                    var fileRes = _fileWriter.AddPlayer(server.Base, cleanName, uuid);
                    if (!fileRes.Success)
                    {
                        return new ApplyWhitelistResult { Success = false, Message = fileRes.Message };
                    }
                }
                break;

            case WhitelistExecutionMode.OnlineOnly: // 模式 2：仅在线时执行
                _commandExecutor.ExecuteAddCommand(config.ServerId, config.CommandTemplate, cleanName, uuid);
                break;

            case WhitelistExecutionMode.QueueReplay: // 模式 3：在线发指令，离线入队开机重放
                if (isOnline)
                {
                    _commandExecutor.ExecuteAddCommand(config.ServerId, config.CommandTemplate, cleanName, uuid);
                }
                else
                {
                    finalStatus = "Pending";
                }
                break;
        }

        // 写入审计记录
        var record = new WhitelistRecord
        {
            ServerId = config.ServerId,
            Qq = cleanQq,
            PlayerName = cleanName,
            PlayerUuid = uuid,
            CreatedAt = DateTime.UtcNow,
            ClientIp = clientIp,
            Status = finalStatus,
            AppliedMode = (int)config.ExecutionMode
        };

        _configService.AddRecord(record);

        // 如果是模式 3 且离线，压入开机就绪重放队列
        if (finalStatus == "Pending")
        {
            _pendingQueue.Enqueue(record);
        }

        var successMsg = finalStatus == "Pending"
            ? $"白名单申请已登记！当前服务器离线，系统将在开机就绪后自动为您载入白名单。"
            : $"恭喜，白名单申请成功！您现在可以使用游戏 ID '{cleanName}' 登录服务器。";

        return new ApplyWhitelistResult
        {
            Success = true,
            Message = successMsg,
            PlayerName = cleanName,
            PlayerUuid = uuid,
            Status = finalStatus
        };
    }

    /// <summary>
    /// 管理员手动撤销白名单
    /// </summary>
    public bool RevokeWhitelist(uint serverId, string recordId)
    {
        var records = _configService.GetRecordsByServerId(serverId);
        var target = records.FirstOrDefault(x => x.Id == recordId);
        if (target == null) return false;

        var server = SDK.MSLX.Config.Servers.GetServer(serverId);
        if (server != null)
        {
            var isOnline = IsServerOnline(serverId);
            if (isOnline)
            {
                _commandExecutor.ExecuteRemoveCommand(serverId, target.PlayerName);
            }
            _fileWriter.RemovePlayer(server.Base, target.PlayerName);
        }

        return _configService.RemoveRecord(serverId, recordId);
    }
}
