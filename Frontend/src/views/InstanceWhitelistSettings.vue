<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { MessagePlugin } from 'tdesign-vue-next';
import {
  LinkIcon,
  BrowseIcon,
  InfoCircleIcon,
  MailIcon,
  SearchIcon,
  RefreshIcon,
  SettingIcon,
} from 'tdesign-icons-vue-next';
import {
  getInstanceWhitelistConfig,
  saveInstanceWhitelistConfig,
  getWhitelistRecords,
  deleteWhitelistRecord,
} from '../api/whitelist';
import SmtpSettingsDialog from './SmtpSettingsDialog.vue';
import type { InstanceWhitelistConfig, WhitelistRecord } from '../types';

const props = defineProps<{
  serverId?: number;
  instanceId?: number;
}>();

const emits = defineEmits<{
  (e: 'success'): void;
  (e: 'saved'): void;
}>();

const currentServerId = computed(() => {
  return props.serverId || props.instanceId || 0;
});

// 获取宿主 v1.7.6+ 节点上下文，子节点自动绑定，未适配/旧宿主平滑回退
const nodeContext = (window as any).MSLX_NodeContext?.use?.();
const httpClient = nodeContext?.request;

const loading = ref(false);
const saving = ref(false);
const smtpDialogRef = ref<InstanceType<typeof SmtpSettingsDialog> | null>(null);

const formData = ref<InstanceWhitelistConfig>({
  serverId: 0,
  enabled: false,
  slug: '',
  title: '',
  notice: '请如实填写 QQ 号及 Minecraft 游戏 ID，严禁作弊或利用白名单扰乱服务器秩序。',
  executionMode: 1, // 1: 改写配置文件 (默认首选), 2: 仅在线, 3: 开启后填充
  commandTemplate: 'whitelist add {username}',
  maxAccountsPerQq: 1,
  allowedNameRegex: '^[a-zA-Z0-9_]{3,16}$',
  codeExpireMinutes: 5,
  codeCooldownSeconds: 60,
  isSmtpConfigured: false,
});

const records = ref<WhitelistRecord[]>([]);
const recordsLoading = ref(false);
const searchKeyword = ref('');

const isSlaveNode = computed(() => {
  return Boolean(nodeContext && nodeContext.nodeId && nodeContext.nodeId !== 'local');
});

const publicBaseUrl = computed(() => {
  try {
    if (isSlaveNode.value) {
      const target = nodeContext.getTarget?.();
      if (target?.baseUrl) {
        return target.baseUrl.replace(/\/+$/, '');
      }
    }
  } catch (err) {
    console.warn('[QqWhitelist] 获取子节点目标地址失败，回退至当前面板', err);
  }
  return typeof window !== 'undefined' ? window.location.origin : '';
});

const fullPublicUrl = computed(() => {
  const s = formData.value.slug.trim() || `server-${currentServerId.value}`;
  return `${publicBaseUrl.value}/public/whitelist/${s}`;
});

const filteredRecords = computed(() => {
  const kw = searchKeyword.value.trim().toLowerCase();
  if (!kw) return records.value;
  return records.value.filter(
    (r) =>
      r.qq.toLowerCase().includes(kw) ||
      r.playerName.toLowerCase().includes(kw) ||
      (r.clientIp && r.clientIp.toLowerCase().includes(kw))
  );
});

const loadConfig = async () => {
  if (!currentServerId.value) return;
  loading.value = true;
  try {
    const res = await getInstanceWhitelistConfig(currentServerId.value, httpClient);
    if (res) {
      formData.value = {
        serverId: currentServerId.value,
        enabled: res.enabled ?? false,
        slug: res.slug || `server-${currentServerId.value}`,
        title: res.title || '',
        notice: res.notice || '请如实填写 QQ 号及 Minecraft 游戏 ID，严禁作弊或利用白名单扰乱服务器秩序。',
        executionMode: res.executionMode || 1,
        commandTemplate: res.commandTemplate || 'whitelist add {username}',
        maxAccountsPerQq: res.maxAccountsPerQq || 1,
        allowedNameRegex: res.allowedNameRegex || '^[a-zA-Z0-9_]{3,16}$',
        codeExpireMinutes: res.codeExpireMinutes || 5,
        codeCooldownSeconds: res.codeCooldownSeconds || 60,
        isSmtpConfigured: res.isSmtpConfigured ?? false,
      };
    }
    await loadRecords();
  } catch (err: any) {
    MessagePlugin.error(err?.message || '获取实例白名单配置失败');
  } finally {
    loading.value = false;
  }
};

const loadRecords = async () => {
  if (!currentServerId.value) return;
  recordsLoading.value = true;
  try {
    const list = await getWhitelistRecords(currentServerId.value, httpClient);
    records.value = Array.isArray(list) ? list : [];
  } catch (err: any) {
    console.error('Failed to load records', err);
  } finally {
    recordsLoading.value = false;
  }
};

watch(
  () => currentServerId.value,
  (val) => {
    if (val) loadConfig();
  },
  { immediate: true }
);

const copyPublicUrl = async () => {
  try {
    await navigator.clipboard.writeText(fullPublicUrl.value);
    MessagePlugin.success('公开申请链接已复制到剪贴板');
  } catch {
    MessagePlugin.warning('复制失败，请手动选中文本复制');
  }
};

const openPublicPreview = () => {
  window.open(fullPublicUrl.value, '_blank');
};

const openSmtpDialog = () => {
  smtpDialogRef.value?.open();
};

const handleSmtpSaved = () => {
  formData.value.isSmtpConfigured = true;
};

const handleRevokeRecord = async (record: WhitelistRecord) => {
  try {
    await deleteWhitelistRecord(currentServerId.value, record.id, httpClient);
    MessagePlugin.success(`已撤销玩家 ${record.playerName} 的白名单`);
    await loadRecords();
  } catch (err: any) {
    MessagePlugin.error(err?.message || '撤销失败');
  }
};

const handleSave = async () => {
  if (!currentServerId.value) return;

  const rawSlug = formData.value.slug.trim();
  if (!/^[a-zA-Z0-9_-]{1,32}$/.test(rawSlug)) {
    MessagePlugin.warning('路径别名仅支持 1-32 位英文字母、数字、下划线及短横线');
    return;
  }

  saving.value = true;
  try {
    await saveInstanceWhitelistConfig(
      currentServerId.value,
      {
        ...formData.value,
        slug: rawSlug,
        title: formData.value.title.trim(),
        notice: formData.value.notice.trim(),
        commandTemplate: formData.value.commandTemplate.trim(),
        allowedNameRegex: formData.value.allowedNameRegex.trim(),
      },
      httpClient
    );
    MessagePlugin.success('白名单设置已成功保存');
    emits('success');
    emits('saved');
  } catch (err: any) {
    MessagePlugin.error(err?.message || '保存设置失败');
  } finally {
    saving.value = false;
  }
};

const tableColumns = [
  { colKey: 'qq', title: 'QQ 号', width: 120 },
  { colKey: 'playerName', title: 'Minecraft 游戏 ID', width: 150 },
  { colKey: 'createdAt', title: '申请时间 (服务器时区)', width: 170 },
  { colKey: 'clientIp', title: '申请 IP', width: 130 },
  { colKey: 'status', title: '状态', width: 100 },
  { colKey: 'action', title: '操作', width: 90 },
];
</script>

<template>
  <div class="instance-whitelist-settings flex flex-col mx-auto w-full pb-8 text-xs sm:text-sm text-[var(--td-text-color-primary)]">
    <t-loading :loading="loading">
      <!-- 提示横幅：SMTP 状态感应 -->
      <div
        v-if="!formData.isSmtpConfigured"
        class="mb-5 p-3.5 rounded-xl bg-amber-500/10 border border-amber-500/20 text-xs text-amber-600 dark:text-amber-400 flex flex-col sm:flex-row sm:items-center justify-between gap-3 leading-relaxed"
      >
        <div class="flex items-start gap-2.5">
          <info-circle-icon class="text-base shrink-0 mt-0.5" />
          <div>
            <strong>发件配置未就绪：</strong>系统尚未配置发信邮箱凭据，外部玩家将无法正常接收 QQ 邮箱验证码。
          </div>
        </div>
        <t-button size="small" theme="warning" variant="base" @click="openSmtpDialog">
          配置全局发信策略
        </t-button>
      </div>

      <!-- 板块一：公开访问与基础配置 -->
      <div class="flex items-center gap-2 mt-2 mb-4 pb-2 border-b border-dashed border-[var(--td-component-border)]">
        <div class="w-1 h-4 bg-[var(--color-primary)] rounded-full"></div>
        <h2 class="text-base font-bold text-[var(--td-text-color-primary)] m-0">公开访问与基础配置</h2>
      </div>

      <!-- 开启开关 -->
      <div class="flex flex-col md:flex-row md:items-start justify-between p-3 md:p-4 border-b border-dashed border-[var(--td-component-border)] hover:bg-[var(--td-bg-color-secondarycontainer)] transition-colors rounded-xl">
        <div class="flex-1 pr-0 md:pr-8 mb-3 md:mb-0 min-w-[200px]">
          <div class="text-sm font-medium text-[var(--td-text-color-primary)] leading-snug">开启白名单自助申请</div>
          <div class="text-xs text-[var(--td-text-color-secondary)] mt-1 leading-relaxed">
            开启后，外部玩家可通过专用网页通道自助验证 QQ 邮箱并添加白名单。
          </div>
        </div>
        <div class="w-full md:w-[340px] shrink-0 flex items-center justify-end">
          <t-switch v-model="formData.enabled" size="large" />
        </div>
      </div>

      <!-- 服务器显示昵称 -->
      <div class="flex flex-col md:flex-row md:items-start justify-between p-3 md:p-4 border-b border-dashed border-[var(--td-component-border)] hover:bg-[var(--td-bg-color-secondarycontainer)] transition-colors rounded-xl">
        <div class="flex-1 pr-0 md:pr-8 mb-3 md:mb-0 min-w-[200px]">
          <div class="text-sm font-medium text-[var(--td-text-color-primary)] leading-snug">服务器展示昵称</div>
          <div class="text-xs text-[var(--td-text-color-secondary)] mt-1 leading-relaxed">
            公开申请页面顶部展示的服务器名称，留空默认使用实例名称。
          </div>
        </div>
        <div class="w-full md:w-[340px] shrink-0">
          <t-input v-model="formData.title" placeholder="留空默认使用实例名称" clearable />
        </div>
      </div>

      <!-- 访问路径 Slug -->
      <div class="flex flex-col md:flex-row md:items-start justify-between p-3 md:p-4 border-b border-dashed border-[var(--td-component-border)] hover:bg-[var(--td-bg-color-secondarycontainer)] transition-colors rounded-xl">
        <div class="flex-1 pr-0 md:pr-8 mb-3 md:mb-0 min-w-[200px]">
          <div class="text-sm font-medium text-[var(--td-text-color-primary)] leading-snug">访问路径别名 (Slug)</div>
          <div class="text-xs text-[var(--td-text-color-secondary)] mt-1 leading-relaxed">
            公开申请页面的 URL 后缀，支持英文字母、数字、下划线及短横线。
          </div>
        </div>
        <div class="w-full md:w-[340px] shrink-0 flex flex-col gap-2">
          <t-input v-model="formData.slug" placeholder="例如 survival" clearable>
            <template #prefix-icon>
              <span class="text-xs text-[var(--td-text-color-placeholder)] select-none">/public/whitelist/</span>
            </template>
          </t-input>

          <!-- 访问预览卡片 -->
          <div class="p-2.5 rounded-lg bg-[var(--td-bg-color-secondarycontainer)] border border-[var(--td-component-border)] text-xs flex flex-col gap-2">
            <div class="flex items-center gap-1.5 text-[var(--td-text-color-secondary)]">
              <link-icon size="14px" />
              <span>公开访问链接：</span>
            </div>
            <div class="font-mono text-[var(--td-text-color-primary)] break-all select-all">
              {{ fullPublicUrl }}
            </div>
            <!-- 若为子节点实例，提示服主公网开放与开关要求 -->
            <div
              v-if="isSlaveNode"
              class="text-[11px] text-blue-600 dark:text-blue-400 flex items-start gap-1.5 leading-relaxed bg-blue-500/10 p-2 rounded border border-blue-500/20"
            >
              <info-circle-icon size="14px" class="shrink-0 mt-0.5" />
              <span>当前为分布式子节点实例，申请链接已自动定向至该子节点独立地址。请确保外部玩家可访问子节点端口，并在子节点开启「公开页面」开关。</span>
            </div>
            <div class="flex items-center gap-2 pt-1 border-t border-[var(--td-component-border)]">
              <t-button size="small" variant="text" theme="primary" @click="copyPublicUrl">
                复制链接
              </t-button>
              <t-button size="small" variant="text" theme="default" @click="openPublicPreview">
                <template #icon><browse-icon /></template>
                打开预览
              </t-button>
            </div>
          </div>
        </div>
      </div>

      <!-- 申请须知与公告 -->
      <div class="flex flex-col md:flex-row md:items-start justify-between p-3 md:p-4 border-b border-dashed border-[var(--td-component-border)] hover:bg-[var(--td-bg-color-secondarycontainer)] transition-colors rounded-xl">
        <div class="flex-1 pr-0 md:pr-8 mb-3 md:mb-0 min-w-[200px]">
          <div class="text-sm font-medium text-[var(--td-text-color-primary)] leading-snug">申请须知与规则公告</div>
          <div class="text-xs text-[var(--td-text-color-secondary)] mt-1 leading-relaxed">
            展示在公开申请页面顶部的提示文案，可用于传达进服要求或注意事项。
          </div>
        </div>
        <div class="w-full md:w-[340px] shrink-0">
          <t-textarea v-model="formData.notice" :autosize="{ minRows: 2, maxRows: 4 }" placeholder="请输入申请须知说明" />
        </div>
      </div>

      <!-- 板块二：白名单执行策略与申请限制 -->
      <div class="flex items-center gap-2 mt-8 mb-4 pb-2 border-b border-dashed border-[var(--td-component-border)]">
        <div class="w-1 h-4 bg-[var(--color-primary)] rounded-full"></div>
        <h2 class="text-base font-bold text-[var(--td-text-color-primary)] m-0">白名单执行策略与规则</h2>
      </div>

      <!-- 申请与执行模式 -->
      <div class="flex flex-col md:flex-row md:items-start justify-between p-3 md:p-4 border-b border-dashed border-[var(--td-component-border)] hover:bg-[var(--td-bg-color-secondarycontainer)] transition-colors rounded-xl">
        <div class="flex-1 pr-0 md:pr-8 mb-3 md:mb-0 min-w-[200px]">
          <div class="text-sm font-medium text-[var(--td-text-color-primary)] leading-snug">白名单处理模式</div>
          <div class="text-xs text-[var(--td-text-color-secondary)] mt-1 leading-relaxed">
            根据服务器架构选择最适宜的白名单处理策略。
          </div>
        </div>
        <div class="w-full md:w-[340px] shrink-0 flex flex-col gap-2">
          <t-select v-model="formData.executionMode">
            <t-option :value="1" label="在线 / 离线均可申请 (改写配置文件模式)" />
            <t-option :value="2" label="仅在线时可申请" />
            <t-option :value="3" label="在线 / 离线均可申请 (开启后填充重放模式)" />
          </t-select>

          <!-- 模式详细策略说明 -->
          <div class="p-2.5 rounded-lg bg-[var(--td-bg-color-secondarycontainer)] border border-[var(--td-component-border)] text-xs text-[var(--td-text-color-secondary)] leading-relaxed">
            <template v-if="formData.executionMode === 1">
              <span class="font-medium text-[var(--td-text-color-primary)]">配置文件模式 (推荐)：</span>
              服务器在线时通过控制台执行指令；离线时直接写入 <code class="px-1 py-0.5 rounded bg-[var(--td-bg-color-component)] font-mono text-[11px]">whitelist.json</code>，支持离线 UUIDv3 与正版 Mojang API 精准映射。
            </template>
            <template v-else-if="formData.executionMode === 2">
              <span class="font-medium text-[var(--td-text-color-primary)]">仅在线模式：</span>
              仅在服务器处于运行状态时接受申请；关机或维护时公共申请页面将直接提示暂停申请。
            </template>
            <template v-else-if="formData.executionMode === 3">
              <span class="font-medium text-[var(--td-text-color-primary)]">开启后填充重放模式：</span>
              离线期间申请的白名单将写入安全队列暂存；等待下一次启动就绪后由后台自动批量执行加入指令。
            </template>
          </div>
        </div>
      </div>

      <!-- 控制台指令模板 -->
      <div class="flex flex-col md:flex-row md:items-start justify-between p-3 md:p-4 border-b border-dashed border-[var(--td-component-border)] hover:bg-[var(--td-bg-color-secondarycontainer)] transition-colors rounded-xl">
        <div class="flex-1 pr-0 md:pr-8 mb-3 md:mb-0 min-w-[200px]">
          <div class="text-sm font-medium text-[var(--td-text-color-primary)] leading-snug">控制台执行指令模板</div>
          <div class="text-xs text-[var(--td-text-color-secondary)] mt-1 leading-relaxed">
            向服务端发送的指令，占位符为 {username} 与 {uuid}；装有 EasyWhitelist 等插件的服务器可自定义为 easywhitelist add {username}。
          </div>
        </div>
        <div class="w-full md:w-[340px] shrink-0">
          <t-input v-model="formData.commandTemplate" placeholder="whitelist add {username}" clearable />
        </div>
      </div>

      <!-- 单 QQ 绑定上限与校验正则 -->
      <div class="flex flex-col md:flex-row md:items-start justify-between p-3 md:p-4 border-b border-dashed border-[var(--td-component-border)] hover:bg-[var(--td-bg-color-secondarycontainer)] transition-colors rounded-xl">
        <div class="flex-1 pr-0 md:pr-8 mb-3 md:mb-0 min-w-[200px]">
          <div class="text-sm font-medium text-[var(--td-text-color-primary)] leading-snug">绑定配额与格式校验</div>
          <div class="text-xs text-[var(--td-text-color-secondary)] mt-1 leading-relaxed">
            限制单个 QQ 号最多可绑定的游戏角色数，以及游戏名的正则格式约束。
          </div>
        </div>
        <div class="w-full md:w-[340px] shrink-0 grid grid-cols-2 gap-3">
          <div class="flex flex-col gap-1">
            <span class="text-[11px] text-[var(--td-text-color-secondary)]">单 QQ 绑定上限</span>
            <t-input-number v-model="formData.maxAccountsPerQq" :min="1" :max="10" theme="column" class="w-full" />
          </div>
          <div class="flex flex-col gap-1">
            <span class="text-[11px] text-[var(--td-text-color-secondary)]">验证码有效期 (分)</span>
            <t-input-number v-model="formData.codeExpireMinutes" :min="1" :max="60" theme="column" class="w-full" />
          </div>
        </div>
      </div>

      <!-- 板块三：发信策略入口 (用户/全局级) -->
      <div class="flex items-center justify-between mt-8 mb-4 pb-2 border-b border-dashed border-[var(--td-component-border)]">
        <div class="flex items-center gap-2">
          <div class="w-1 h-4 bg-[var(--color-primary)] rounded-full"></div>
          <h2 class="text-base font-bold text-[var(--td-text-color-primary)] m-0">全局发信策略配置</h2>
        </div>
        <t-button size="small" variant="outline" theme="primary" @click="openSmtpDialog">
          <template #icon><setting-icon /></template>
          配置发信策略 (SMTP)
        </t-button>
      </div>

      <div class="p-4 rounded-xl border border-[var(--td-component-border)] bg-[var(--td-bg-color-secondarycontainer)] flex items-center justify-between">
        <div class="flex items-center gap-3">
          <mail-icon size="24px" class="text-blue-500" />
          <div>
            <div class="font-medium text-xs sm:text-sm">
              发件通道状态：
              <span v-if="formData.isSmtpConfigured" class="text-emerald-500 font-bold">已配置就绪</span>
              <span v-else class="text-amber-500 font-bold">尚未配置</span>
            </div>
            <div class="text-xs text-[var(--td-text-color-secondary)] mt-0.5">
              用于向玩家下发验证邮件的 SMTP 服务器。支持随时在此弹窗中修改参数并发送测试邮件。
            </div>
          </div>
        </div>
        <t-button size="small" variant="base" theme="default" @click="openSmtpDialog">
          管理发件凭据
        </t-button>
      </div>

      <!-- 板块四：白名单申请记录与审计 -->
      <div class="flex items-center justify-between mt-8 mb-4 pb-2 border-b border-dashed border-[var(--td-component-border)]">
        <div class="flex items-center gap-2">
          <div class="w-1 h-4 bg-[var(--color-primary)] rounded-full"></div>
          <h2 class="text-base font-bold text-[var(--td-text-color-primary)] m-0">申请记录与审计</h2>
        </div>
        <div class="flex items-center gap-2">
          <t-input
            v-model="searchKeyword"
            placeholder="搜索 QQ 号或游戏 ID"
            clearable
            size="small"
            class="w-48"
          >
            <template #prefix-icon><search-icon /></template>
          </t-input>
          <t-button size="small" variant="text" theme="default" @click="loadRecords">
            <template #icon><refresh-icon /></template>
          </t-button>
        </div>
      </div>

      <!-- 数据表格（防爆容器包裹） -->
      <div class="overflow-x-auto rounded-xl border border-[var(--td-component-border)]">
        <t-table
          row-key="id"
          :data="filteredRecords"
          :columns="tableColumns"
          :loading="recordsLoading"
          empty="暂无白名单申请记录"
          size="small"
        >
          <template #qq="{ row }">
            <span class="font-mono">{{ row.qq }}</span>
          </template>
          <template #playerName="{ row }">
            <span class="font-medium text-[var(--td-text-color-primary)]">{{ row.playerName }}</span>
          </template>
          <template #createdAt="{ row }">
            <span class="text-xs font-mono text-[var(--td-text-color-secondary)]">{{ row.createdAt }}</span>
          </template>
          <template #clientIp="{ row }">
            <span class="font-mono text-xs text-[var(--td-text-color-secondary)]">{{ row.clientIp || '-' }}</span>
          </template>
          <template #status="{ row }">
            <t-tag v-if="row.status === 'Active'" theme="success" variant="light" size="small">生效中</t-tag>
            <t-tag v-else-if="row.status === 'Pending'" theme="warning" variant="light" size="small">待生效</t-tag>
            <t-tag v-else theme="default" variant="light" size="small">已撤销</t-tag>
          </template>
          <template #action="{ row }">
            <t-popconfirm
              content="确认撤销该玩家的白名单资格吗？"
              theme="danger"
              @confirm="handleRevokeRecord(row)"
            >
              <t-button size="small" variant="text" theme="danger">
                撤销
              </t-button>
            </t-popconfirm>
          </template>
        </t-table>
      </div>

      <!-- 保存操作栏 -->
      <div class="mt-8 pt-4 flex items-center justify-end gap-3 border-t border-[var(--td-component-border)]">
        <t-button theme="default" variant="base" @click="loadConfig">
          重置配置
        </t-button>
        <t-button theme="primary" :loading="saving" @click="handleSave">
          保存设置
        </t-button>
      </div>
    </t-loading>

    <!-- 发信策略弹窗 -->
    <smtp-settings-dialog ref="smtpDialogRef" @saved="handleSmtpSaved" />
  </div>
</template>

<style scoped>
.instance-whitelist-settings {
  width: 100%;
}
</style>
