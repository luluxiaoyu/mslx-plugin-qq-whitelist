import request from 'mslx-request';
import type {
  InstanceWhitelistConfig,
  SmtpConfig,
  WhitelistRecord,
  PublicServerInfo,
  ApplyWhitelistResult,
} from '../types';

const BASE_URL = '/api/plugin/mslx-plugin-qq-whitelist/whitelist';

// 管理端：获取实例白名单配置
export async function getInstanceWhitelistConfig(serverId: number): Promise<InstanceWhitelistConfig> {
  return await request.get({
    url: `${BASE_URL}/instance-config/${serverId}`,
  });
}

// 管理端：保存实例白名单配置
export async function saveInstanceWhitelistConfig(serverId: number, data: Partial<InstanceWhitelistConfig>): Promise<void> {
  return await request.post({
    url: `${BASE_URL}/instance-config/${serverId}`,
    data,
  });
}

// 管理端：获取全局 SMTP 发信策略
export async function getSmtpConfig(): Promise<SmtpConfig> {
  return await request.get({
    url: `${BASE_URL}/smtp-config`,
  });
}

// 管理端：保存全局 SMTP 发信策略
export async function saveSmtpConfig(data: Partial<SmtpConfig>): Promise<void> {
  return await request.post({
    url: `${BASE_URL}/smtp-config`,
    data,
  });
}

// 管理端：一键发送测试邮件
export async function testSmtpEmail(targetEmail: string, temporaryConfig?: Partial<SmtpConfig>): Promise<string> {
  const res: any = await request.post({
    url: `${BASE_URL}/test-smtp`,
    data: {
      targetEmail,
      smtpConfig: temporaryConfig,
    },
  });
  return res?.message || '测试邮件已发送';
}

// 管理端：获取实例白名单审计记录
export async function getWhitelistRecords(serverId: number): Promise<WhitelistRecord[]> {
  return await request.get({
    url: `${BASE_URL}/records/${serverId}`,
  });
}

// 管理端：撤销白名单
export async function deleteWhitelistRecord(serverId: number, recordId: string): Promise<void> {
  return await request.delete({
    url: `${BASE_URL}/record/${serverId}/${recordId}`,
  });
}

// 公开端：获取公开申请页面信息
export async function getPublicServerInfo(params: { slug?: string; serverId?: number }): Promise<PublicServerInfo> {
  const qs: string[] = [];
  if (params.slug) qs.push(`slug=${encodeURIComponent(params.slug)}`);
  if (params.serverId) qs.push(`serverId=${encodeURIComponent(params.serverId)}`);

  return await request.get({
    url: `${BASE_URL}/public/info${qs.length ? '?' + qs.join('&') : ''}`,
  });
}

// 公开端：发送 QQ 邮箱验证码
export async function sendPublicCode(payload: { slug?: string; serverId?: number; qq: string }): Promise<string> {
  const res: any = await request.post({
    url: `${BASE_URL}/public/send-code`,
    data: payload,
  });
  return res?.message || '验证码已发送至 QQ 邮箱';
}

// 公开端：提交白名单申请
export async function submitPublicApply(payload: {
  slug?: string;
  serverId?: number;
  qq: string;
  code: string;
  username: string;
}): Promise<ApplyWhitelistResult> {
  return await request.post({
    url: `${BASE_URL}/public/apply`,
    data: payload,
  });
}
