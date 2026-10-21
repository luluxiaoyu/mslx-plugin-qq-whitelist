namespace MSLX.Plugin.QqWhitelist.Models;

/// <summary>
/// 白名单申请与执行模式
/// </summary>
public enum WhitelistExecutionMode
{
    /// <summary>
    /// 模式 1（默认）：在线/离线均可申请（改写配置文件模式）
    /// 在线时发指令并重载，离线时根据离线/正版 UUID 算法精准读写 whitelist.json
    /// </summary>
    DirectFile = 1,

    /// <summary>
    /// 模式 2：仅在线时可申请
    /// 仅当服务器完全启动就绪时才允许提交，关机时阻断申请并提示维护
    /// </summary>
    OnlineOnly = 2,

    /// <summary>
    /// 模式 3：在线/离线均可申请（开启后填充模式）
    /// 离线时登记为待处理，开机捕获 Done 日志就绪后自动批量补发控制台指令
    /// </summary>
    QueueReplay = 3
}

/// <summary>
/// 实例级别的白名单配置模型
/// </summary>
public class InstanceWhitelistConfig
{
    /// <summary>
    /// 关联的实例 ID
    /// </summary>
    public uint ServerId { get; set; }

    /// <summary>
    /// 是否开启自助申请白名单
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// 自定义公开 URL 访问别名（如 survival -> /public/whitelist/survival）
    /// </summary>
    public string Slug { get; set; } = "";

    /// <summary>
    /// 服务器自定义显示昵称（展示在公开申请页面顶栏，留空则默认使用实例名）
    /// </summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// 申请须知与规则公告（展示在公开页面的提示卡片）
    /// </summary>
    public string Notice { get; set; } = "请如实填写 QQ 号及 Minecraft 游戏 ID，严禁作弊或利用白名单扰乱服务器秩序。";

    /// <summary>
    /// 白名单申请与执行模式（默认模式 1：改写配置文件）
    /// </summary>
    public WhitelistExecutionMode ExecutionMode { get; set; } = WhitelistExecutionMode.DirectFile;

    /// <summary>
    /// 控制台执行的白名单指令模板（支持占位符：{username}, {uuid}）
    /// </summary>
    public string CommandTemplate { get; set; } = "whitelist add {username}";

    /// <summary>
    /// 单个 QQ 号允许绑定的最大游戏角色数量（默认 1）
    /// </summary>
    public int MaxAccountsPerQq { get; set; } = 1;

    /// <summary>
    /// 游戏名格式校验正则表达式（默认 Minecraft Java 版 3-16 位字母数字下划线）
    /// </summary>
    public string AllowedNameRegex { get; set; } = @"^[a-zA-Z0-9_]{3,16}$";

    /// <summary>
    /// 验证码有效时长（分钟，默认 5）
    /// </summary>
    public int CodeExpireMinutes { get; set; } = 5;

    /// <summary>
    /// 发送验证码冷却时间（秒，默认 60）
    /// </summary>
    public int CodeCooldownSeconds { get; set; } = 60;
}
