<script setup lang="ts">
import { ref } from 'vue';
import { MessagePlugin } from 'tdesign-vue-next';
import {
  MailIcon,
  CheckCircleIcon,
  LockOnIcon,
  SendIcon,
  ServerIcon,
  BrowseIcon,
  BrowseOffIcon,
} from 'tdesign-icons-vue-next';
import { getSmtpConfig, saveSmtpConfig, testSmtpEmail } from '../api/whitelist';
import type { SmtpConfig } from '../types';

const visible = ref(false);
const loading = ref(false);
const saving = ref(false);
const testing = ref(false);
const showPassword = ref(false);

const formData = ref<SmtpConfig>({
  host: 'smtp.qq.com',
  port: 465,
  enableSsl: true,
  fromEmail: '',
  fromName: 'MSLX 白名单助手',
  username: '',
  password: '',
  hasPassword: false,
  subjectTemplate: '【{server_name}】白名单申请验证码',
  customHtmlTemplate: '',
});

const testTargetEmail = ref('');
const testFeedback = ref<{ success: boolean; message: string } | null>(null);

const emits = defineEmits<{
  (e: 'saved'): void;
}>();

const open = async () => {
  visible.value = true;
  testFeedback.value = null;
  loading.value = true;
  try {
    const res = await getSmtpConfig();
    if (res) {
      formData.value = {
        host: res.host || 'smtp.qq.com',
        port: res.port || 465,
        enableSsl: res.enableSsl ?? true,
        fromEmail: res.fromEmail || '',
        fromName: res.fromName || 'MSLX 白名单助手',
        username: res.username || '',
        password: '',
        hasPassword: res.hasPassword ?? false,
        subjectTemplate: res.subjectTemplate || '【{server_name}】白名单申请验证码',
        customHtmlTemplate: res.customHtmlTemplate || '',
      };
    }
  } catch (err: any) {
    MessagePlugin.error(err?.message || '获取发信策略配置失败');
  } finally {
    loading.value = false;
  }
};

defineExpose({ open });

const applyPreset = (type: 'qq' | '163' | 'outlook') => {
  if (type === 'qq') {
    formData.value.host = 'smtp.qq.com';
    formData.value.port = 465;
    formData.value.enableSsl = true;
    MessagePlugin.success('已填入 QQ 邮箱 SMTP 标准参数（端口 465 SSL）');
  } else if (type === '163') {
    formData.value.host = 'smtp.163.com';
    formData.value.port = 465;
    formData.value.enableSsl = true;
    MessagePlugin.success('已填入 163 网易邮箱 SMTP 标准参数（端口 465 SSL）');
  } else if (type === 'outlook') {
    formData.value.host = 'smtp.office365.com';
    formData.value.port = 587;
    formData.value.enableSsl = true;
    MessagePlugin.success('已填入 Outlook / Microsoft 365 标准参数（端口 587 STARTTLS）');
  }
};

const handleTestEmail = async () => {
  const target = testTargetEmail.value.trim();
  if (!target || !target.includes('@')) {
    MessagePlugin.warning('请输入有效的测试收件邮箱地址');
    return;
  }

  testing.value = true;
  testFeedback.value = null;
  try {
    const msg = await testSmtpEmail(target, {
      host: formData.value.host,
      port: formData.value.port,
      enableSsl: formData.value.enableSsl,
      fromEmail: formData.value.fromEmail,
      fromName: formData.value.fromName,
      username: formData.value.username,
      password: formData.value.password,
    });
    testFeedback.value = { success: true, message: msg };
    MessagePlugin.success('测试邮件发送成功，请查收收件箱');
  } catch (err: any) {
    testFeedback.value = { success: false, message: err?.message || '测试连接失败' };
    MessagePlugin.error(err?.message || '测试邮件发送失败');
  } finally {
    testing.value = false;
  }
};

const handleSave = async () => {
  if (!formData.value.host.trim()) {
    MessagePlugin.warning('请输入 SMTP 服务器地址');
    return;
  }
  if (!formData.value.fromEmail.trim()) {
    MessagePlugin.warning('请输入发件人邮箱地址');
    return;
  }
  if (!formData.value.hasPassword && !formData.value.password?.trim()) {
    MessagePlugin.warning('请输入发信认证密码或授权码');
    return;
  }

  saving.value = true;
  try {
    await saveSmtpConfig(formData.value);
    MessagePlugin.success('发信策略已成功保存');
    visible.value = false;
    emits('saved');
  } catch (err: any) {
    MessagePlugin.error(err?.message || '保存发信策略失败');
  } finally {
    saving.value = false;
  }
};
</script>

<template>
  <t-dialog
    attach="body"
    v-model:visible="visible"
    header="全局发信策略配置 (SMTP)"
    width="min(640px, 92vw)"
    :footer="false"
  >
    <t-loading :loading="loading">
      <div class="smtp-dialog-content flex flex-col gap-4 text-xs sm:text-sm text-[var(--td-text-color-primary)]">
        <!-- 浏览器密码管理器蜜罐拦截 (Anti-Autofill Honeypot) -->
        <div style="position: absolute; width: 0; height: 0; overflow: hidden; opacity: 0; pointer-events: none;" aria-hidden="true">
          <input type="text" name="fake_username_honeypot" tabindex="-1" autocomplete="off" />
          <input type="password" name="fake_password_honeypot" tabindex="-1" autocomplete="new-password" />
        </div>

        <!-- 提示栏 -->
        <div class="p-3 rounded-xl bg-blue-500/10 border border-blue-500/20 text-xs text-blue-600 dark:text-blue-400 leading-relaxed">
          发信策略属于宿主全局配置，所有开启白名单申请的实例将统一复用该发件通道，无需为每个实例重复配置。
        </div>

        <!-- 常用预设快捷按钮 -->
        <div class="flex flex-wrap items-center gap-2">
          <span class="text-xs text-[var(--td-text-color-secondary)]">快速填充参数:</span>
          <t-button size="small" variant="outline" theme="default" @click="applyPreset('qq')">
            QQ 邮箱 (465 SSL)
          </t-button>
          <t-button size="small" variant="outline" theme="default" @click="applyPreset('163')">
            163 邮箱 (465 SSL)
          </t-button>
          <t-button size="small" variant="outline" theme="default" @click="applyPreset('outlook')">
            Outlook / M365 (587 TLS)
          </t-button>
        </div>

        <!-- 参数表单 -->
        <div class="flex flex-col gap-3 p-4 rounded-xl border border-[var(--td-component-border)] bg-[var(--td-bg-color-secondarycontainer)]">
          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div class="sm:col-span-2 flex flex-col gap-1.5">
              <label class="text-xs font-medium text-[var(--td-text-color-secondary)]">SMTP 服务器地址</label>
              <t-input v-model="formData.host" placeholder="例如 smtp.qq.com" clearable>
                <template #prefix-icon><server-icon /></template>
              </t-input>
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-medium text-[var(--td-text-color-secondary)]">端口号</label>
              <t-input-number v-model="formData.port" :min="1" :max="65535" theme="column" class="w-full" />
            </div>
          </div>

          <div class="flex items-center justify-between py-1">
            <div>
              <div class="text-xs font-medium">启用 SSL / TLS 安全加密</div>
              <div class="text-[11px] text-[var(--td-text-color-secondary)]">465 端口请开启 SSL；587 端口走 STARTTLS</div>
            </div>
            <t-switch v-model="formData.enableSsl" />
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-medium text-[var(--td-text-color-secondary)]">发件人邮箱</label>
              <t-input
                v-model="formData.fromEmail"
                placeholder="例如 bot@qq.com"
                name="smtp_from_email_field"
                autocomplete="off"
                clearable
              >
                <template #prefix-icon><mail-icon /></template>
              </t-input>
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-medium text-[var(--td-text-color-secondary)]">发件人显示名称</label>
              <t-input
                v-model="formData.fromName"
                placeholder="例如 MSLX 白名单助手"
                name="smtp_from_name_field"
                autocomplete="off"
                clearable
              />
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-medium text-[var(--td-text-color-secondary)]">认证账号 (Username)</label>
              <t-input
                v-model="formData.username"
                placeholder="留空默认同发件人邮箱"
                name="smtp_auth_user_field"
                autocomplete="off"
                autocapitalize="off"
                spellcheck="false"
                clearable
              />
            </div>
            <div class="flex flex-col gap-1.5">
              <label class="text-xs font-medium text-[var(--td-text-color-secondary)]">认证密码 / 邮箱授权码</label>
              <t-input
                v-model="formData.password"
                :type="showPassword ? 'text' : 'password'"
                :placeholder="formData.hasPassword ? '已配置授权码，留空保持不变' : '请输入授权码'"
                name="smtp_auth_token_field"
                autocomplete="new-password"
                autocapitalize="off"
                spellcheck="false"
                clearable
              >
                <template #prefix-icon><lock-on-icon /></template>
                <template #suffix-icon>
                  <span
                    class="cursor-pointer text-[var(--td-text-color-placeholder)] hover:text-[var(--td-text-color-primary)] transition-colors flex items-center"
                    @click="showPassword = !showPassword"
                    :title="showPassword ? '隐藏授权码' : '显示授权码'"
                  >
                    <browse-icon v-if="showPassword" />
                    <browse-off-icon v-else />
                  </span>
                </template>
              </t-input>
            </div>
          </div>

          <div class="flex flex-col gap-1.5">
            <label class="text-xs font-medium text-[var(--td-text-color-secondary)]">邮件主题模板</label>
            <t-input v-model="formData.subjectTemplate" placeholder="支持占位符 {server_name}" clearable />
          </div>
        </div>

        <!-- 发信连通性测试模块 -->
        <div class="p-3.5 rounded-xl border border-[var(--td-component-border)] bg-[var(--td-bg-color-container)] flex flex-col gap-2.5">
          <div class="flex items-center justify-between">
            <div class="text-xs font-bold text-[var(--td-text-color-primary)] flex items-center gap-1.5">
              <send-icon class="text-blue-500" />
              <span>测试发信通道连通性</span>
            </div>
          </div>
          <div class="flex items-center gap-2">
            <t-input
              v-model="testTargetEmail"
              placeholder="输入接收测试邮件的邮箱地址"
              class="flex-1"
              clearable
            />
            <t-button theme="primary" variant="outline" :loading="testing" @click="handleTestEmail">
              发送测试邮件
            </t-button>
          </div>
          <div
            v-if="testFeedback"
            :class="[
              'p-2.5 rounded-lg text-xs leading-relaxed flex items-start gap-2',
              testFeedback.success
                ? 'bg-emerald-500/10 border border-emerald-500/20 text-emerald-600 dark:text-emerald-400'
                : 'bg-rose-500/10 border border-rose-500/20 text-rose-600 dark:text-rose-400'
            ]"
          >
            <check-circle-icon v-if="testFeedback.success" class="shrink-0 mt-0.5" />
            <div class="break-all">{{ testFeedback.message }}</div>
          </div>
        </div>

        <!-- 底部保存操作栏 -->
        <div class="pt-3 flex items-center justify-end gap-2 border-t border-[var(--td-component-border)]">
          <t-button theme="default" variant="base" @click="visible = false">取消</t-button>
          <t-button theme="primary" :loading="saving" @click="handleSave">保存发信策略</t-button>
        </div>
      </div>
    </t-loading>
  </t-dialog>
</template>

<style scoped>
.smtp-dialog-content {
  width: 100%;
}
</style>
