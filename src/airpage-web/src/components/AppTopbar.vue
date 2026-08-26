<script setup>
defineProps({title:String,subtitle:String,busy:Boolean})
defineEmits(['refresh','logout'])
</script>
<template>
 <header class="topbar"><div><span class="eyebrow">AIRPAGE CONTROL CENTER</span><h1>{{title}}</h1><p>{{subtitle}}</p></div><div class="topbar-actions"><button class="ghost icon-button" :disabled="busy" @click="$emit('refresh')">↻<span>刷新</span></button><button class="ghost" @click="$emit('logout')">退出登录</button></div></header>
</template>
