namespace MSLX.Plugin.QqWhitelist.Models;

/// <summary>
/// 白名单申请与审计记录
/// </summary>
public class WhitelistRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public uint ServerId { get; set; }
    public string Qq { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public string PlayerUuid { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ClientIp { get; set; } = "";
    
    /// <summary>
    /// 状态：Active (生效中), Pending (待开机就绪补发), Revoked (已撤销/移除)
    /// </summary>
    public string Status { get; set; } = "Active";

    /// <summary>
    /// 申请时所用的模式
    /// </summary>
    public int AppliedMode { get; set; } = 1;
}
