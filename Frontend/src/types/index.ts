export interface InstanceWhitelistConfig {
  serverId: number;
  enabled: boolean;
  slug: string;
  title: string;
  notice: string;
  executionMode: number; // 1: 在线离线均可(改写文件), 2: 仅在线, 3: 在线离线均可(开启后填充)
  commandTemplate: string;
  maxAccountsPerQq: number;
  allowedNameRegex: string;
  codeExpireMinutes: number;
  codeCooldownSeconds: number;
  isSmtpConfigured: boolean;
}

export interface SmtpConfig {
  host: string;
  port: number;
  enableSsl: boolean;
  fromEmail: string;
  fromName: string;
  username: string;
  password?: string;
  hasPassword?: boolean;
  subjectTemplate: string;
  customHtmlTemplate?: string;
}

export interface WhitelistRecord {
  id: string;
  serverId: number;
  qq: string;
  playerName: string;
  playerUuid: string;
  createdAt: string;
  clientIp: string;
  status: 'Active' | 'Pending' | 'Revoked' | string;
  appliedMode: number;
}

export interface PublicServerInfo {
  serverId: number;
  title: string;
  slug: string;
  notice: string;
  enabled: boolean;
  isServerOnline: boolean;
  executionMode: number;
  maxAccountsPerQq: number;
  allowedNameRegex: string;
  hasServerIcon?: boolean;
  serverIconUrl?: string;
}

export interface ApplyWhitelistResult {
  success: boolean;
  message: string;
  playerName: string;
  playerUuid: string;
  status: string;
}
