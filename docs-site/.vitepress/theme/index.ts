import DefaultTheme from 'vitepress/theme';
import { h } from 'vue';
import PageSource from './PageSource.vue';
import './style.css';

export default {
    extends: DefaultTheme,
    Layout: () => h(DefaultTheme.Layout, null, { 'doc-before': () => h(PageSource) }),
};
