namespace MSLX.Plugin.QqWhitelist.Models;

/// <summary>
/// 内存中的验证码会话对象
/// </summary>
public class VerificationSession
{
    public uint ServerId { get; set; }
    public string Qq { get; set; } = "";
    public string Code { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public string ClientIp { get; set; } = "";
    public int AttemptCount { get; set; } = 0;
}
