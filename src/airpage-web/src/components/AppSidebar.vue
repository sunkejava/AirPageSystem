<script setup>
defineProps({items:Array,active:String,user:Object,defaultDevice:Object,version:String})
defineEmits(['navigate'])
</script>
<template>
 <aside class="sidebar">
  <div class="brand"><span class="mark">A</span><div><b>AirPage</b><small>Studio · v{{version}}</small></div></div>
  <nav><button v-for="item in items" :key="item[0]" :class="{active:active===item[0]}" @click="$emit('navigate',item[0])"><i>{{item[2]}}</i><span>{{item[1]}}</span></button></nav>
  <div class="sidebar-status"><span :class="['dot',defaultDevice?'ok':'']"></span><div><small>{{user?.displayName}}</small><b>{{defaultDevice?.name||'尚未配置设备'}}</b></div></div>
 </aside>
</template>
