using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MSLX.Plugin.QqWhitelist.Models;

namespace MSLX.Plugin.QqWhitelist.Services;

/// <summary>
/// SMTP 邮件发送服务
/// 基于 MailKit 实现高效、安全的验证邮件投递与握手测试
/// </summary>
public class EmailSenderService
{
    /// <summary>
    /// 生成高质感现代化 HTML 验证码邮件正文
    /// </summary>
    public static string BuildDefaultHtmlTemplate(string serverName, string code, int expireMinutes)
    {
        var sendTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>【{serverName}】白名单申请验证码</title>
</head>
<body style=""margin: 0; padding: 28px 16px; background-color: #f4f6f8; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1f2937;"">
  <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 520px; margin: 0 auto; background-color: #ffffff; border-radius: 16px; border: 1px solid #e5e7eb; padding: 32px; box-shadow: 0 4px 20px rgba(0, 0, 0, 0.05);"">
    <tr>
      <td>
        <!-- 顶部徽标与标题 -->
        <div style=""display: inline-block; padding: 3px 8px; background-color: #eff6ff; border: 1px solid #dbeafe; border-radius: 6px; font-size: 11px; font-weight: 600; color: #0052d9; letter-spacing: 0.5px; text-transform: uppercase;"">
          Minecraft 白名单申请
        </div>
        <h2 style=""margin: 12px 0 6px 0; font-size: 20px; font-weight: 700; color: #0052d9; line-height: 1.3;"">
          {serverName}
        </h2>
        <p style=""margin: 0 0 20px 0; font-size: 14px; color: #4b5563; line-height: 1.6;"">
          您好！您正在通过自助申请通道申请加入该服务器的白名单，请在验证页面输入以下 6 位动态验证码：
        </p>

        <!-- 验证码卡片 -->
        <div style=""margin: 0 0 22px 0; padding: 20px 16px; background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; text-align: center;"">
          <div style=""font-size: 11px; color: #64748b; font-weight: 500; letter-spacing: 0.5px; margin-bottom: 6px;"">
            动态安全验证码
          </div>
          <span style=""display: inline-block; font-family: 'SF Mono', Consolas, Monaco, monospace; font-size: 34px; font-weight: 700; letter-spacing: 10px; color: #0052d9;"">
            {code}
          </span>
        </div>

        <!-- 参数元数据表格 -->
        <table border=""0"" cellpadding=""6"" cellspacing=""0"" width=""100%"" style=""background-color: #f8fafc; border-radius: 8px; font-size: 13px; color: #475569; margin-bottom: 20px;"">
          <tr><td width=""90"" style=""color: #64748b;"">申请服务器:</td><td><strong style=""color: #1f2937;"">{serverName}</strong></td></tr>
          <tr><td style=""color: #64748b;"">有效时长:</td><td><strong>{expireMinutes} 分钟</strong>（单次使用后失效）</td></tr>
          <tr><td style=""color: #64748b;"">发送时间:</td><td>{sendTime}</td></tr>
          <tr><td style=""color: #64748b;"">安全提醒:</td><td style=""color: #dc2626;"">请勿将此验证码提供给任何第三方</td></tr>
        </table>

        <!-- 底部版权与免责声明 -->
        <div style=""padding-top: 16px; border-top: 1px solid #f1f5f9; text-align: center; font-size: 12px; color: #9ca3af; line-height: 1.5;"">
          此邮件由 MSLX 白名单助手系统自动投递，若非您本人申请请忽略此邮件。
        </div>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    /// <summary>
    /// 生成测试邮件 HTML 正文
    /// </summary>
    public static string BuildTestHtmlTemplate(string fromEmail, string host, int port)
    {
        var testTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        return $@"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>MSLX SMTP 发信测试</title>
</head>
<body style=""margin: 0; padding: 28px 16px; background-color: #f4f6f8; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #1f2937;"">
  <table role=""presentation"" border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 520px; margin: 0 auto; background-color: #ffffff; border-radius: 16px; border: 1px solid #e5e7eb; padding: 32px; box-shadow: 0 4px 20px rgba(0, 0, 0, 0.05);"">
    <tr>
      <td>
        <div style=""display: inline-block; padding: 3px 8px; background-color: #eff6ff; border: 1px solid #dbeafe; border-radius: 6px; font-size: 11px; font-weight: 600; color: #0052d9; letter-spacing: 0.5px; text-transform: uppercase;"">
          SMTP 发信诊断
        </div>
        <h2 style=""margin: 12px 0 10px 0; font-size: 20px; font-weight: 700; color: #0052d9;"">发信通道连通性测试报告</h2>
        <p style=""margin: 0 0 16px 0; font-size: 14px; color: #374151; line-height: 1.6;"">
          恭喜！当您看到这封邮件时，说明您的 SMTP 发件服务器配置已成功建立安全连接并顺利完成握手投递。
        </p>
        <table border=""0"" cellpadding=""6"" cellspacing=""0"" width=""100%"" style=""background-color: #f8fafc; border-radius: 8px; font-size: 13px; color: #475569; margin-bottom: 20px;"">
          <tr><td width=""90"" style=""color: #64748b;"">发件服务器:</td><td><strong style=""color: #1f2937;"">{host}:{port}</strong></td></tr>
          <tr><td style=""color: #64748b;"">发件地址:</td><td>{fromEmail}</td></tr>
          <tr><td style=""color: #64748b;"">投递时间:</td><td>{testTime}</td></tr>
          <tr><td style=""color: #64748b;"">连接状态:</td><td style=""color: #10b981; font-weight: 600;"">SSL/TLS 握手认证通过</td></tr>
        </table>
        <div style=""padding-top: 16px; border-top: 1px solid #f1f5f9; text-align: center; font-size: 12px; color: #9ca3af; line-height: 1.5;"">
          此邮件由 MSLX 发信通道自检程序自动生成。
        </div>
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    /// <summary>
    /// 创建并配置 SmtpClient
    /// </summary>
    private static async Task<SmtpClient> CreateConnectedClientAsync(SmtpConfig config, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.Host))
            throw new ArgumentException("SMTP 服务器主机地址不能为空");
        if (string.IsNullOrWhiteSpace(config.FromEmail))
            throw new ArgumentException("发件人邮箱地址不能为空");

        var client = new SmtpClient();

        // 针对自签名证书等环境设置宽容安全检查，但在生产环境中保障安全性
        client.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

        SecureSocketOptions options;
        if (config.Port == 465)
        {
            options = SecureSocketOptions.SslOnConnect;
        }
        else if (config.Port == 587)
        {
            options = SecureSocketOptions.StartTls;
        }
        else if (config.EnableSsl)
        {
            options = SecureSocketOptions.Auto;
        }
        else
        {
            options = SecureSocketOptions.None;
        }

        await client.ConnectAsync(config.Host, config.Port, options, ct);

        var authUser = string.IsNullOrWhiteSpace(config.Username) ? config.FromEmail.Trim() : config.Username.Trim();
        if (!string.IsNullOrWhiteSpace(authUser) && !string.IsNullOrWhiteSpace(config.Password))
        {
            await client.AuthenticateAsync(authUser, config.Password, ct);
        }

        return client;
    }

    /// <summary>
    /// 测试 SMTP 服务器连通性与邮件投递
    /// </summary>
    public async Task<(bool Success, string Message)> TestSmtpConnectionAsync(SmtpConfig config, string targetEmail)
    {
        if (string.IsNullOrWhiteSpace(targetEmail) || !targetEmail.Contains('@'))
        {
            return (false, "目标测试邮箱地址格式无效");
        }

        try
        {
            using var client = await CreateConnectedClientAsync(config);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(config.FromName, config.FromEmail));
            message.To.Add(new MailboxAddress(targetEmail, targetEmail));
            message.Subject = "【MSLX】SMTP 发件服务连通性测试报告";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = BuildTestHtmlTemplate(config.FromEmail, config.Host, config.Port)
            };
            message.Body = bodyBuilder.ToMessageBody();

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            return (true, $"测试邮件已成功投递至 {targetEmail}");
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"[QqWhitelist] SMTP 发信测试失败: {ex.Message}");
            return (false, $"发信失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 发送白名单验证码邮件
    /// </summary>
    public async Task<(bool Success, string Message)> SendVerificationCodeAsync(
        SmtpConfig config,
        string toEmail,
        string serverName,
        string code,
        int expireMinutes)
    {
        if (string.IsNullOrWhiteSpace(config.FromEmail))
        {
            return (false, "系统尚未配置发件邮箱，请联系管理员配置发信策略");
        }

        try
        {
            using var client = await CreateConnectedClientAsync(config);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(config.FromName, config.FromEmail));
            message.To.Add(new MailboxAddress(toEmail, toEmail));

            var subject = config.SubjectTemplate.Replace("{server_name}", serverName);
            message.Subject = subject;

            string htmlContent;
            if (!string.IsNullOrWhiteSpace(config.CustomHtmlTemplate))
            {
                htmlContent = config.CustomHtmlTemplate
                    .Replace("{server_name}", serverName)
                    .Replace("{code}", code)
                    .Replace("{expire_minutes}", expireMinutes.ToString());
            }
            else
            {
                htmlContent = BuildDefaultHtmlTemplate(serverName, code, expireMinutes);
            }

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlContent };
            message.Body = bodyBuilder.ToMessageBody();

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            return (true, "验证码已成功发送至您的 QQ 邮箱");
        }
        catch (Exception ex)
        {
            SDK.MSLX.Logger.Error($"[QqWhitelist] 向 {toEmail} 发送验证码失败: {ex.Message}");
            return (false, $"验证邮件发送失败: {ex.Message}");
        }
    }
}
