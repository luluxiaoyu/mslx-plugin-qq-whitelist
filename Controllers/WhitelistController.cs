using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MSLX.Plugin.QqWhitelist.Models;
using MSLX.Plugin.QqWhitelist.Services;

namespace MSLX.Plugin.QqWhitelist.Controllers;

[ApiController]
[Route("api/plugin/mslx-plugin-qq-whitelist/whitelist")]
public class WhitelistController : ControllerBase
{
    private readonly QqWhitelistConfigService _configService;
    private readonly WhitelistApplicationService _applicationService;
    private readonly EmailSenderService _emailService;

    public WhitelistController(
        QqWhitelistConfigService configService,
        WhitelistApplicationService applicationService,
        EmailSenderService emailService)
    {
        _configService = configService;
        _applicationService = applicationService;
        _emailService = emailService;
    }

    private string GetClientIp()
    {
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var ip = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(ip)) return ip;
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    #region 管理端接口 (需要管理权限)

    [HttpGet("instance-config/{serverId}")]
    public IActionResult GetInstanceConfig(uint serverId)
    {
        try
        {
            var config = _configService.GetInstanceConfig(serverId);
            var dto = new InstanceWhitelistConfigDto
            {
                ServerId = config.ServerId,
                Enabled = config.Enabled,
                Slug = config.Slug,
                Title = config.Title,
                Notice = config.Notice,
                ExecutionMode = (int)config.ExecutionMode,
                CommandTemplate = config.CommandTemplate,
                MaxAccountsPerQq = config.MaxAccountsPerQq,
                AllowedNameRegex = config.AllowedNameRegex,
                CodeExpireMinutes = config.CodeExpireMinutes,
                CodeCooldownSeconds = config.CodeCooldownSeconds,
                IsSmtpConfigured = _configService.IsSmtpConfigured()
            };

            return Ok(new { code = 200, data = dto, message = "获取成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    [HttpPost("instance-config/{serverId}")]
    public IActionResult SaveInstanceConfig(uint serverId, [FromBody] InstanceWhitelistConfigDto dto)
    {
        try
        {
            _configService.SaveInstanceConfig(serverId, dto);
            return Ok(new { code = 200, message = "设置已保存" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    [HttpGet("smtp-config")]
    public IActionResult GetSmtpConfig()
    {
        try
        {
            var cfg = _configService.GetSmtpConfig();
            var dto = new SmtpConfigDto
            {
                Host = cfg.Host,
                Port = cfg.Port,
                EnableSsl = cfg.EnableSsl,
                FromEmail = cfg.FromEmail,
                FromName = cfg.FromName,
                Username = cfg.Username,
                Password = "", // 密码敏感脱敏
                HasPassword = !string.IsNullOrWhiteSpace(cfg.Password),
                SubjectTemplate = cfg.SubjectTemplate,
                CustomHtmlTemplate = cfg.CustomHtmlTemplate
            };

            return Ok(new { code = 200, data = dto, message = "获取成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    [HttpPost("smtp-config")]
    public IActionResult SaveSmtpConfig([FromBody] SmtpConfigDto dto)
    {
        try
        {
            _configService.SaveSmtpConfig(dto);
            return Ok(new { code = 200, message = "全局发信策略已保存" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    [HttpPost("test-smtp")]
    public async Task<IActionResult> TestSmtp([FromBody] TestEmailRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.TargetEmail))
            {
                return BadRequest(new { code = 400, message = "请输入有效的收件测试邮箱" });
            }

            // 若请求自带当前表单的临时 SMTP 参数，使用表单参数测试；否则使用已保存的配置
            SmtpConfig smtp;
            if (request.SmtpConfig != null && !string.IsNullOrWhiteSpace(request.SmtpConfig.Host))
            {
                var saved = _configService.GetSmtpConfig();
                var fromEmail = request.SmtpConfig.FromEmail.Trim();
                smtp = new SmtpConfig
                {
                    Host = request.SmtpConfig.Host.Trim(),
                    Port = request.SmtpConfig.Port,
                    EnableSsl = request.SmtpConfig.EnableSsl,
                    FromEmail = fromEmail,
                    FromName = string.IsNullOrWhiteSpace(request.SmtpConfig.FromName) ? "MSLX 白名单助手" : request.SmtpConfig.FromName.Trim(),
                    Username = string.IsNullOrWhiteSpace(request.SmtpConfig.Username) ? fromEmail : request.SmtpConfig.Username.Trim(),
                    Password = string.IsNullOrWhiteSpace(request.SmtpConfig.Password) 
                        ? saved.Password 
                        : request.SmtpConfig.Password.Trim()
                };
            }
            else
            {
                smtp = _configService.GetSmtpConfig();
            }

            var (success, msg) = await _emailService.TestSmtpConnectionAsync(smtp, request.TargetEmail.Trim());
            if (success)
            {
                return Ok(new { code = 200, message = msg });
            }

            return BadRequest(new { code = 400, message = msg });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = $"测试发信异常: {ex.Message}" });
        }
    }

    [HttpGet("records/{serverId}")]
    public IActionResult GetRecords(uint serverId)
    {
        try
        {
            var records = _configService.GetRecordsByServerId(serverId);
            // 确保输出时间与服务器本机时区一致 (yyyy-MM-dd HH:mm:ss)
            var dtos = records.Select(r => new
            {
                r.Id,
                r.ServerId,
                r.Qq,
                r.PlayerName,
                r.PlayerUuid,
                CreatedAt = r.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                r.ClientIp,
                r.Status,
                r.AppliedMode
            });
            return Ok(new { code = 200, data = dtos, message = "获取成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    [HttpDelete("record/{serverId}/{recordId}")]
    public IActionResult DeleteRecord(uint serverId, string recordId)
    {
        try
        {
            var success = _applicationService.RevokeWhitelist(serverId, recordId);
            if (success)
            {
                return Ok(new { code = 200, message = "白名单已成功撤销" });
            }

            return BadRequest(new { code = 400, message = "目标记录不存在或撤销失败" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    #endregion

    #region 公开免登录接口 ([AllowAnonymous])

    [HttpGet("public/info")]
    [AllowAnonymous]
    public IActionResult GetPublicInfo([FromQuery] string? slug, [FromQuery] uint? serverId)
    {
        try
        {
            var info = _applicationService.GetPublicInfo(slug, serverId);
            if (info == null)
            {
                return NotFound(new { code = 404, message = "未找到该服务器的白名单申请通道或该通道已关闭" });
            }

            return Ok(new { code = 200, data = info, message = "获取成功" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    /// <summary>
    /// 公开安全获取服务器真实图标 (server-icon.png)
    /// </summary>
    [HttpGet("public/server-icon.png")]
    [AllowAnonymous]
    public IActionResult GetPublicServerIcon([FromQuery] string? slug, [FromQuery] uint? serverId)
    {
        try
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

            // 安全检查 1：仅当白名单已明确开启时才允许公开读取图标，杜绝未授权实例信息探测
            if (config == null || !config.Enabled)
            {
                return NotFound();
            }

            var server = SDK.MSLX.Config.Servers.GetServer(config.ServerId);
            if (server == null || string.IsNullOrWhiteSpace(server.Base))
            {
                return NotFound();
            }

            var baseDir = Path.GetFullPath(server.Base);
            var iconPath = Path.GetFullPath(Path.Combine(baseDir, "server-icon.png"));

            // 安全检查 2：沙箱越界与路径穿越防御
            if (!iconPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            var fileInfo = new FileInfo(iconPath);
            if (!fileInfo.Exists)
            {
                return NotFound();
            }

            // 安全检查 3：文件大小阈值保护（标准 64x64 通常为数 KB，拦截超过 1MB 的超大文件拒绝服务风险）
            if (fileInfo.Length > 1024 * 1024)
            {
                return BadRequest();
            }

            // 安全检查 4：PNG 文件头魔数强校验（防御扩展名伪造及恶意脚本或 HTML 注入）
            using (var fs = System.IO.File.OpenRead(iconPath))
            {
                byte[] header = new byte[8];
                int read = fs.Read(header, 0, 8);
                byte[] pngMagic = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
                if (read < 8 || !header.SequenceEqual(pngMagic))
                {
                    return BadRequest();
                }
            }

            Response.Headers.Append("Cache-Control", "public, max-age=3600");
            Response.Headers.Append("X-Content-Type-Options", "nosniff");
            return PhysicalFile(iconPath, "image/png");
        }
        catch
        {
            return NotFound();
        }
    }

    [HttpPost("public/send-code")]
    [AllowAnonymous]
    public async Task<IActionResult> SendCode([FromBody] SendCodeRequest request)
    {
        try
        {
            var clientIp = GetClientIp();
            var (success, msg) = await _applicationService.SendVerificationCodeAsync(
                request.Slug,
                request.ServerId,
                request.Qq,
                clientIp
            );

            if (success)
            {
                return Ok(new { code = 200, message = msg });
            }

            return BadRequest(new { code = 400, message = msg });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = $"发送验证码失败: {ex.Message}" });
        }
    }

    [HttpPost("public/apply")]
    [AllowAnonymous]
    public async Task<IActionResult> Apply([FromBody] ApplyWhitelistRequest request)
    {
        try
        {
            var clientIp = GetClientIp();
            var result = await _applicationService.ApplyWhitelistAsync(
                request.Slug,
                request.ServerId,
                request.Qq,
                request.Code,
                request.Username,
                clientIp
            );

            if (result.Success)
            {
                return Ok(new { code = 200, data = result, message = result.Message });
            }

            return BadRequest(new { code = 400, message = result.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = $"申请白名单失败: {ex.Message}" });
        }
    }

    #endregion
}
