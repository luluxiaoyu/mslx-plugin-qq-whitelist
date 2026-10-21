import InstanceWhitelistSettings from './views/InstanceWhitelistSettings.vue';
import PublicWhitelistPage from './views/PublicWhitelistPage.vue';
import './style.css';
import { UserAddIcon } from 'tdesign-icons-vue-next';

export const pluginConfig = {
  name: 'MSLX.Plugin.QqWhitelist',
  version: '1.0.0',

  // 免登录独立全屏公开页面路由 (v1.7.3+)
  routes: [
    {
      path: '/public/whitelist',
      name: 'PublicWhitelistRoot',
      component: PublicWhitelistPage,
      meta: {
        title: '自助申请白名单',
        anonymous: true,
        hidden: true,
      },
    },
    {
      path: '/public/whitelist/:slug',
      name: 'PublicWhitelistSlug',
      component: PublicWhitelistPage,
      meta: {
        title: '自助申请白名单',
        anonymous: true,
        hidden: true,
      },
    },
    {
      path: '/mslx-plugin-qq-whitelist/public',
      name: 'MslxPluginQqWhitelistPublic',
      component: PublicWhitelistPage,
      meta: {
        title: '自助申请白名单',
        anonymous: true,
        hidden: true,
      },
    },
    {
      path: '/mslx-plugin-qq-whitelist/public/:slug',
      name: 'MslxPluginQqWhitelistPublicSlug',
      component: PublicWhitelistPage,
      meta: {
        title: '自助申请白名单',
        anonymous: true,
        hidden: true,
      },
    },
  ],

  // UI 扩展插槽注入：挂载在实例设置弹窗侧边栏
  extensions: [
    {
      slot: 'instance-settings-tab',
      component: InstanceWhitelistSettings,
      label: 'QQ白名单',
      icon: UserAddIcon,
    },
  ],
};
