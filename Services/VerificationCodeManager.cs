using System.Collections.Concurrent;
using System.Security.Cryptography;
using MSLX.Plugin.QqWhitelist.Models;

namespace MSLX.Plugin.QqWhitelist.Services;

/// <summary>
/// 验证码生成、校验与防刷限流管理器
/// </summary>
public class VerificationCodeManager
{
    private readonly ConcurrentDictionary<string, VerificationSession> _sessions = new();
    private readonly ConcurrentDictionary<string, DateTime> _cooldowns = new();

    private static string BuildKey(uint serverId, string qq) => $"{serverId}:{qq.Trim()}";

    /// <summary>
    /// 获取当前 QQ 或 IP 的剩余冷却时间（秒）
    /// </summary>
    public int GetRemainingCooldownSeconds(uint serverId, string qq, string clientIp)
    {
        var now = DateTime.UtcNow;
        var qqKey = $"qq:{BuildKey(serverId, qq)}";
        var ipKey = $"ip:{serverId}:{clientIp.Trim()}";

        var qqRemaining = 0;
        if (_cooldowns.TryGetValue(qqKey, out var qqCooldown) && qqCooldown > now)
        {
            qqRemaining = (int)Math.Ceiling((qqCooldown - now).TotalSeconds);
        }

        var ipRemaining = 0;
        if (!string.IsNullOrWhiteSpace(clientIp) &&
            _cooldowns.TryGetValue(ipKey, out var ipCooldown) && ipCooldown > now)
        {
            ipRemaining = (int)Math.Ceiling((ipCooldown - now).TotalSeconds);
        }

        return Math.Max(qqRemaining, ipRemaining);
    }

    /// <summary>
    /// 生成 6 位随机验证码并记录会话
    /// </summary>
    public (bool Success, string Code, string Message) GenerateCode(
        uint serverId,
        string qq,
        string clientIp,
        int expireMinutes = 5,
        int cooldownSeconds = 60)
    {
        var remaining = GetRemainingCooldownSeconds(serverId, qq, clientIp);
        if (remaining > 0)
        {
            return (false, "", $"操作过于频繁，请在 {remaining} 秒后再试");
        }

        // 生成高强度 6 位纯数字验证码
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var now = DateTime.UtcNow;

        var session = new VerificationSession
        {
            ServerId = serverId,
            Qq = qq.Trim(),
            Code = code,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(expireMinutes),
            ClientIp = clientIp.Trim(),
            AttemptCount = 0
        };

        var sessionKey = BuildKey(serverId, qq);
        _sessions[sessionKey] = session;

        // 设置防刷冷却
        var cdTime = now.AddSeconds(cooldownSeconds);
        _cooldowns[$"qq:{sessionKey}"] = cdTime;
        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            _cooldowns[$"ip:{serverId}:{clientIp.Trim()}"] = cdTime;
        }

        // 清理超期数据
        CleanupExpired();

        return (true, code, "验证码生成成功");
    }

    /// <summary>
    /// 校验验证码（单次有效，带防爆破保护）
    /// </summary>
    public (bool Valid, string Message) VerifyCode(uint serverId, string qq, string inputCode)
    {
        var sessionKey = BuildKey(serverId, qq);
        if (!_sessions.TryGetValue(sessionKey, out var session))
        {
            return (false, "验证码不存在或已过期，请重新获取");
        }

        if (DateTime.UtcNow > session.ExpiresAt)
        {
            _sessions.TryRemove(sessionKey, out _);
            return (false, "验证码已过期，请重新获取");
        }

        session.AttemptCount++;
        if (session.AttemptCount > 5)
        {
            _sessions.TryRemove(sessionKey, out _);
            return (false, "验证失败次数过多，该验证码已被作废，请重新获取");
        }

        if (!string.Equals(session.Code.Trim(), inputCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var remainingAttempts = 5 - session.AttemptCount;
            return (false, $"验证码错误，还可尝试 {remainingAttempts} 次");
        }

        // 验证通过，立即移除（阅后即焚）
        _sessions.TryRemove(sessionKey, out _);
        return (true, "验证成功");
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _sessions)
        {
            if (kvp.Value.ExpiresAt < now)
            {
                _sessions.TryRemove(kvp.Key, out _);
            }
        }

        foreach (var kvp in _cooldowns)
        {
            if (kvp.Value < now)
            {
                _cooldowns.TryRemove(kvp.Key, out _);
            }
        }
    }
}
