namespace MSLX.Plugin.QqWhitelist.Models;

/// <summary>
/// 实例白名单配置 DTO
/// </summary>
public class InstanceWhitelistConfigDto
{
    public uint ServerId { get; set; }
    public bool Enabled { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Notice { get; set; } = "";
    public int ExecutionMode { get; set; } = 1;
    public string CommandTemplate { get; set; } = "whitelist add {username}";
    public int MaxAccountsPerQq { get; set; } = 1;
    public string AllowedNameRegex { get; set; } = @"^[a-zA-Z0-9_]{3,16}$";
    public int CodeExpireMinutes { get; set; } = 5;
    public int CodeCooldownSeconds { get; set; } = 60;
    public bool IsSmtpConfigured { get; set; }
}

/// <summary>
/// 全局 SMTP 配置 DTO（密码脱敏）
/// </summary>
public class SmtpConfigDto
{
    public string Host { get; set; } = "smtp.qq.com";
    public int Port { get; set; } = 465;
    public bool EnableSsl { get; set; } = true;
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "MSLX 白名单助手";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool HasPassword { get; set; } = false;
    public string SubjectTemplate { get; set; } = "【{server_name}】白名单申请验证码";
    public string CustomHtmlTemplate { get; set; } = "";
}

/// <summary>
/// 测试发送邮件请求
/// </summary>
public class TestEmailRequest
{
    public string TargetEmail { get; set; } = "";
    public SmtpConfigDto? SmtpConfig { get; set; }
}

/// <summary>
/// 公开页面查询返回数据
/// </summary>
public class PublicServerInfoDto
{
    public uint ServerId { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Notice { get; set; } = "";
    public bool Enabled { get; set; }
    public bool IsServerOnline { get; set; }
    public int ExecutionMode { get; set; }
    public int MaxAccountsPerQq { get; set; }
    public string AllowedNameRegex { get; set; } = "";
    public bool HasServerIcon { get; set; }
    public string? ServerIconUrl { get; set; }
}

/// <summary>
/// 发送验证码请求
/// </summary>
public class SendCodeRequest
{
    public string? Slug { get; set; }
    public uint? ServerId { get; set; }
    public string Qq { get; set; } = "";
}

/// <summary>
/// 提交白名单申请请求
/// </summary>
public class ApplyWhitelistRequest
{
    public string? Slug { get; set; }
    public uint? ServerId { get; set; }
    public string Qq { get; set; } = "";
    public string Code { get; set; } = "";
    public string Username { get; set; } = "";
}

/// <summary>
/// 申请白名单结果
/// </summary>
public class ApplyWhitelistResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string PlayerUuid { get; set; } = "";
    public string Status { get; set; } = "Active";
}
