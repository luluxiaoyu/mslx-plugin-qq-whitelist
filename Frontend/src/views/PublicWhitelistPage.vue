<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import { MessagePlugin } from 'tdesign-vue-next';
import {
  CheckCircleFilledIcon,
  ErrorCircleFilledIcon,
  MailIcon,
  UserIcon,
  LockOnIcon,
  ServerIcon,
  InfoCircleIcon,
  SecuredIcon,
} from 'tdesign-icons-vue-next';
import {
  getPublicServerInfo,
  sendPublicCode,
  submitPublicApply,
} from '../api/whitelist';
import type { PublicServerInfo, ApplyWhitelistResult } from '../types';

const route = useRoute();

const loading = ref(true);
const serverInfo = ref<PublicServerInfo | null>(null);
const errorMessage = ref('');

// 表单输入
const inputQq = ref('');
const inputCode = ref('');
const inputUsername = ref('');

// 验证码倒计时状态
const countdown = ref(0);
let timer: any = null;
const sendingCode = ref(false);

// 提交状态
const submitting = ref(false);
const applyResult = ref<ApplyWhitelistResult | null>(null);

// 玩家头像预览加载失败占位
const avatarError = ref(false);

// 服务器真实图标加载失败占位
const serverIconError = ref(false);

const currentSlug = computed(() => {
  const p = route.params.slug;
  if (Array.isArray(p)) return p[0] || '';
  return (p as string) || '';
});

const qqMailHint = computed(() => {
  const q = inputQq.value.trim();
  if (!q) return '请输入您的常用 QQ 号码';
  return `验证码邮件将发送至 ${q}@qq.com`;
});

const avatarUrl = computed(() => {
  const u = inputUsername.value.trim();
  if (!u || u.length < 3) return '';
  return `https://mc-heads.net/avatar/${encodeURIComponent(u)}/64`;
});

watch(
  () => inputUsername.value,
  () => {
    avatarError.value = false;
  }
);

const fetchInfo = async () => {
  loading.value = true;
  errorMessage.value = '';
  applyResult.value = null;
  serverIconError.value = false;

  try {
    const slug = currentSlug.value;
    const res = await getPublicServerInfo({ slug: slug || undefined });
    if (res && res.enabled) {
      serverInfo.value = res;
    } else {
      errorMessage.value = '该服务器未开启白名单申请通道，或访问路径不存在。';
    }
  } catch (err: any) {
    errorMessage.value = err?.message || '获取服务器白名单通道信息失败';
  } finally {
    loading.value = false;
  }
};

onMounted(() => {
  fetchInfo();
});

watch(
  () => route.params.slug,
  () => {
    fetchInfo();
  }
);

const handleSendCode = async () => {
  const q = inputQq.value.trim();
  if (!/^[1-9]\d{4,9}$/.test(q)) {
    MessagePlugin.warning('请输入有效的 5-10 位 QQ 号码（首位非 0）');
    return;
  }

  if (countdown.value > 0) return;

  sendingCode.value = true;
  try {
    const msg = await sendPublicCode({
      slug: currentSlug.value || undefined,
      serverId: serverInfo.value?.serverId,
      qq: q,
    });
    MessagePlugin.success(msg || '验证码已发送至 QQ 邮箱');

    // 启动 60 秒倒计时
    countdown.value = 60;
    if (timer) clearInterval(timer);
    timer = setInterval(() => {
      countdown.value--;
      if (countdown.value <= 0) {
        clearInterval(timer);
      }
    }, 1000);
  } catch (err: any) {
    MessagePlugin.error(err?.message || '发送验证码失败');
  } finally {
    sendingCode.value = false;
  }
};

const handleSubmit = async () => {
  const q = inputQq.value.trim();
  const c = inputCode.value.trim();
  const u = inputUsername.value.trim();

  if (!/^[1-9]\d{4,9}$/.test(q)) {
    MessagePlugin.warning('请输入有效的 5-10 位 QQ 号码（首位非 0）');
    return;
  }

  if (!/^\d{6}$/.test(c)) {
    MessagePlugin.warning('请输入 6 位数字验证码');
    return;
  }

  if (!u) {
    MessagePlugin.warning('请输入 Minecraft 游戏角色名');
    return;
  }

  if (serverInfo.value?.allowedNameRegex) {
    const regex = new RegExp(serverInfo.value.allowedNameRegex);
    if (!regex.test(u)) {
      MessagePlugin.warning('游戏角色名不符合规范（仅限 3-16 位字母、数字及下划线）');
      return;
    }
  }

  submitting.value = true;
  try {
    const res = await submitPublicApply({
      slug: currentSlug.value || undefined,
      serverId: serverInfo.value?.serverId,
      qq: q,
      code: c,
      username: u,
    });

    applyResult.value = res;
    MessagePlugin.success(res.message || '白名单申请成功');
  } catch (err: any) {
    MessagePlugin.error(err?.message || '申请白名单失败');
  } finally {
    submitting.value = false;
  }
};

const handleReset = () => {
  applyResult.value = null;
  inputCode.value = '';
  inputUsername.value = '';
};
</script>

<template>
  <div class="public-whitelist-wrapper flex-1 w-full flex flex-col items-center justify-center p-4 sm:p-6">

    <!-- 加载中 -->
    <div v-if="loading" class="relative z-10 flex flex-col items-center justify-center gap-3">
      <t-loading size="large" />
      <span class="text-xs text-slate-400">正在加载服务器白名单通道...</span>
    </div>

    <!-- 错误状态 -->
    <div
      v-else-if="errorMessage"
      class="relative z-10 max-w-md w-full p-8 rounded-2xl border border-rose-500/20 bg-rose-500/10 backdrop-blur-xl text-center flex flex-col items-center gap-3"
    >
      <error-circle-filled-icon size="48px" class="text-rose-500" />
      <h3 class="text-lg font-bold text-rose-600 dark:text-rose-400 m-0">通道不可用</h3>
      <p class="text-xs sm:text-sm text-slate-600 dark:text-slate-300 leading-relaxed m-0">
        {{ errorMessage }}
      </p>
    </div>

    <!-- 成功结果展示 -->
    <div
      v-else-if="applyResult"
      class="design-card relative z-10 max-w-md w-full p-6 sm:p-8 rounded-3xl border border-[var(--td-component-border)] shadow-xl flex flex-col items-center text-center gap-4 animate-fade-in backdrop-blur-xl"
    >
      <div class="w-16 h-16 rounded-full bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center text-emerald-500">
        <check-circle-filled-icon size="40px" />
      </div>

      <div>
        <h2 class="text-xl font-bold text-[var(--td-text-color-primary)] m-0">
          {{ applyResult.status === 'Pending' ? '申请已成功登记' : '白名单申请成功' }}
        </h2>
        <p class="text-xs sm:text-sm text-[var(--td-text-color-secondary)] mt-1 leading-relaxed">
          {{ applyResult.message }}
        </p>
      </div>

      <!-- 绑定信息卡片 -->
      <div class="w-full p-4 rounded-2xl bg-[var(--td-bg-color-secondarycontainer)] border border-[var(--td-component-border)] flex flex-col gap-2.5 text-xs text-left">
        <div class="flex items-center justify-between">
          <span class="text-[var(--td-text-color-placeholder)]">服务器:</span>
          <span class="font-bold text-[var(--td-text-color-primary)]">{{ serverInfo?.title }}</span>
        </div>
        <div class="flex items-center justify-between">
          <span class="text-[var(--td-text-color-placeholder)]">已授权角色:</span>
          <span class="font-mono font-bold text-blue-600 dark:text-blue-400 text-sm">{{ applyResult.playerName }}</span>
        </div>
        <div class="flex items-center justify-between">
          <span class="text-[var(--td-text-color-placeholder)]">绑定 QQ:</span>
          <span class="font-mono text-[var(--td-text-color-primary)]">{{ inputQq }}</span>
        </div>
        <div class="flex items-center justify-between">
          <span class="text-[var(--td-text-color-placeholder)]">UUID 映射:</span>
          <span class="font-mono text-[11px] text-[var(--td-text-color-secondary)] truncate max-w-[200px]" :title="applyResult.playerUuid">
            {{ applyResult.playerUuid }}
          </span>
        </div>
        <div class="flex items-center justify-between">
          <span class="text-[var(--td-text-color-placeholder)]">状态:</span>
          <span v-if="applyResult.status === 'Pending'" class="text-amber-500 font-medium">待开机自动载入</span>
          <span v-else class="text-emerald-500 font-medium">已即时生效</span>
        </div>
      </div>

      <div class="w-full pt-2 flex flex-col gap-2">
        <t-button block theme="primary" size="large" @click="handleReset">
          为其他角色申请
        </t-button>
      </div>
    </div>

    <!-- 主表单卡片 -->
    <div
      v-else
      class="design-card relative z-10 max-w-lg w-full p-6 sm:p-8 rounded-3xl border border-[var(--td-component-border)] shadow-2xl flex flex-col gap-6 backdrop-blur-xl"
    >
      <!-- 头部：服务器信息与在线状态 -->
      <div class="flex flex-col gap-2 border-b border-slate-100 dark:border-slate-800/80 pb-5">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-3">
            <!-- 真实服务器图标 (server-icon.png) / 回退默认图标 -->
            <div
              v-if="serverInfo?.serverIconUrl && !serverIconError"
              class="w-11 h-11 rounded-2xl overflow-hidden border border-slate-200/80 dark:border-slate-700/80 bg-slate-100 dark:bg-slate-800 shrink-0 shadow-md flex items-center justify-center p-0.5"
            >
              <img
                :src="serverInfo.serverIconUrl"
                alt="Server Icon"
                class="w-full h-full object-contain rounded-xl"
                @error="serverIconError = true"
              />
            </div>
            <div
              v-else
              class="w-11 h-11 rounded-2xl bg-blue-600 flex items-center justify-center text-white shadow-md shadow-blue-500/20 shrink-0"
            >
              <server-icon size="22px" />
            </div>
            <div>
              <h1 class="text-lg sm:text-xl font-bold tracking-tight text-slate-900 dark:text-white m-0 leading-tight">
                {{ serverInfo?.title }}
              </h1>
              <div class="flex items-center gap-1.5 mt-0.5">
                <span class="text-[11px] text-slate-400">自助白名单认证中心</span>
              </div>
            </div>
          </div>
          <!-- 运行状态指示徽标 -->
          <div
            :class="[
              'px-2.5 py-1 rounded-full text-[11px] font-medium flex items-center gap-1.5 border',
              serverInfo?.isServerOnline
                ? 'bg-emerald-500/10 border-emerald-500/20 text-emerald-600 dark:text-emerald-400'
                : 'bg-slate-500/10 border-slate-500/20 text-slate-500 dark:text-slate-400'
            ]"
          >
            <span :class="['w-1.5 h-1.5 rounded-full', serverInfo?.isServerOnline ? 'bg-emerald-500 animate-pulse' : 'bg-slate-400']"></span>
            <span>{{ serverInfo?.isServerOnline ? '服务器运行中' : '服务器离线中' }}</span>
          </div>
        </div>

        <!-- 申请须知公告 -->
        <div
          v-if="serverInfo?.notice"
          class="mt-2 p-3 rounded-xl bg-blue-50/60 dark:bg-slate-800/50 border border-blue-100 dark:border-slate-700/60 text-xs text-slate-600 dark:text-slate-300 leading-relaxed flex items-start gap-2"
        >
          <info-circle-icon class="text-blue-500 shrink-0 mt-0.5 text-sm" />
          <div class="break-words">{{ serverInfo.notice }}</div>
        </div>
      </div>

      <!-- 表单区域 -->
      <div class="flex flex-col gap-4">
        <!-- 浏览器密码管理器蜜罐拦截 (Anti-Autofill Honeypot) -->
        <div style="position: absolute; width: 0; height: 0; overflow: hidden; opacity: 0; pointer-events: none;" aria-hidden="true">
          <input type="text" name="fake_username_honeypot" tabindex="-1" autocomplete="off" />
          <input type="password" name="fake_password_honeypot" tabindex="-1" autocomplete="new-password" />
        </div>

        <!-- 步骤 1: QQ 号码 -->
        <div class="flex flex-col gap-1.5">
          <label class="text-xs font-semibold text-slate-700 dark:text-slate-300 flex items-center justify-between">
            <span>QQ 号码</span>
            <span class="text-[11px] font-normal text-slate-400">{{ qqMailHint }}</span>
          </label>
          <div class="flex items-center gap-2">
            <t-input
              v-model="inputQq"
              placeholder="请输入您的 5-10 位 QQ 号"
              size="large"
              maxlength="10"
              name="applicant_qq_field"
              autocomplete="off"
              autocapitalize="off"
              spellcheck="false"
              clearable
              class="flex-1"
            >
              <template #prefix-icon><mail-icon class="text-slate-400" /></template>
            </t-input>
            <t-button
              size="large"
              theme="primary"
              variant="outline"
              :loading="sendingCode"
              :disabled="countdown > 0"
              @click="handleSendCode"
              class="shrink-0 min-w-[110px]"
            >
              {{ countdown > 0 ? `${countdown}s 后可重发` : '获取验证码' }}
            </t-button>
          </div>
        </div>

        <!-- 步骤 2: 验证码输入 -->
        <div class="flex flex-col gap-1.5">
          <label class="text-xs font-semibold text-slate-700 dark:text-slate-300">
            6 位邮箱验证码
          </label>
          <t-input
            v-model="inputCode"
            placeholder="请输入 QQ 邮箱收到的 6 位验证码"
            size="large"
            maxlength="6"
            name="whitelist_otp_code"
            autocomplete="one-time-code"
            autocapitalize="off"
            spellcheck="false"
            inputmode="numeric"
            clearable
          >
            <template #prefix-icon><lock-on-icon class="text-slate-400" /></template>
          </t-input>
        </div>

        <!-- 步骤 3: 游戏角色名 -->
        <div class="flex flex-col gap-1.5">
          <label class="text-xs font-semibold text-slate-700 dark:text-slate-300">
            Minecraft 游戏角色名 (Game ID)
          </label>
          <div class="flex items-center gap-3">
            <t-input
              v-model="inputUsername"
              placeholder="例如 Steve / Alex"
              size="large"
              name="minecraft_username_field"
              autocomplete="off"
              autocapitalize="off"
              spellcheck="false"
              clearable
              class="flex-1"
            >
              <template #prefix-icon><user-icon class="text-slate-400" /></template>
            </t-input>

            <!-- 皮肤头像实时预览 -->
            <div
              v-if="inputUsername.length >= 3 && !avatarError"
              class="w-10 h-10 rounded-lg overflow-hidden border border-slate-200 dark:border-slate-700 shrink-0 bg-slate-100 dark:bg-slate-800 flex items-center justify-center shadow-inner"
              title="实时头像预览"
            >
              <img
                :src="avatarUrl"
                alt="Head"
                class="w-full h-full object-cover"
                @error="avatarError = true"
              />
            </div>
          </div>
        </div>

        <!-- 提交按钮 -->
        <div class="pt-2 flex flex-col gap-2">
          <t-button
            block
            size="large"
            theme="primary"
            :loading="submitting"
            @click="handleSubmit"
            class="h-12 text-sm font-semibold tracking-wide"
          >
            <template #icon><secured-icon /></template>
            提交申请并加入白名单
          </t-button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.public-whitelist-wrapper {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
}
</style>
