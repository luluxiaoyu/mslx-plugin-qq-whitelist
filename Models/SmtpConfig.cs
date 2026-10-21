namespace MSLX.Plugin.QqWhitelist.Models;

/// <summary>
/// 全局 SMTP 邮件发送配置
/// </summary>
public class SmtpConfig
{
    /// <summary>
    /// SMTP 服务器主机地址（如 smtp.qq.com）
    /// </summary>
    public string Host { get; set; } = "smtp.qq.com";

    /// <summary>
    /// 端口号（465 SSL, 587 STARTTLS）
    /// </summary>
    public int Port { get; set; } = 465;

    /// <summary>
    /// 是否启用 SSL / TLS
    /// </summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>
    /// 发件人邮箱地址
    /// </summary>
    public string FromEmail { get; set; } = "";

    /// <summary>
    /// 发件人显示名称
    /// </summary>
    public string FromName { get; set; } = "MSLX 白名单助手";

    /// <summary>
    /// SMTP 认证用户名（通常同发件人邮箱）
    /// </summary>
    public string Username { get; set; } = "";

    /// <summary>
    /// SMTP 认证密码 / 邮箱授权码
    /// </summary>
    public string Password { get; set; } = "";

    /// <summary>
    /// 邮件主题模板（支持占位符：{server_name}）
    /// </summary>
    public string SubjectTemplate { get; set; } = "【{server_name}】白名单申请验证码";

    /// <summary>
    /// 邮件正文 HTML 模板（留空则使用内置标准高级模板，支持占位符：{server_name}, {code}, {expire_minutes}）
    /// </summary>
    public string CustomHtmlTemplate { get; set; } = "";
}
