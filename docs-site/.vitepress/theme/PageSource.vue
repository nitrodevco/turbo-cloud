<script setup lang="ts">
import { useData, withBase } from 'vitepress';
import { computed, ref } from 'vue';

// Every page's markdown is published beside it (<page>.md), for people and for AI tools.
const { page } = useData();
const source = computed(() => withBase('/' + page.value.relativePath));
const label = ref('Copy as markdown');

async function copy() {
    try {
        const response = await fetch(source.value);
        if (!response.ok) throw new Error(String(response.status));

        await navigator.clipboard.writeText(await response.text());
        label.value = 'Copied';
    } catch {
        label.value = 'Could not copy';
    }

    setTimeout(() => (label.value = 'Copy as markdown'), 2000);
}
</script>

<template>
    <div class="page-source">
        <button type="button" @click="copy">{{ label }}</button>
        <a :href="source" target="_blank" rel="noopener">View as markdown</a>
    </div>
</template>
