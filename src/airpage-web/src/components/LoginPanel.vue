<script setup>
import {reactive} from 'vue'
defineProps({busy:Boolean,version:{type:String,default:'…'}})
const emit=defineEmits(['login'])
const form=reactive({username:'admin',password:''})
function submit(){emit('login',{...form});form.password=''}
</script>
<template>
 <main class="login-page">
  <div class="login-decoration"><span>2-bit</span><b>528 × 792</b><small>让重要信息安静地留在屏幕上</small></div>
  <form class="login-card" @submit.prevent="submit">
   <div class="login-logo"><span class="mark">A</span><div><b>AirPageSystem</b><small>Snapshot Studio · v{{version}}</small></div></div>
   <div class="login-title"><h1>欢迎回来</h1><p>登录后管理你的设备、面板和推送任务</p></div>
   <label>用户名<input v-model.trim="form.username" autocomplete="username" autofocus></label>
   <label>密码<input v-model="form.password" type="password" autocomplete="current-password"></label>
   <button class="login-submit" :disabled="busy">{{busy?'正在验证…':'安全登录'}}</button>
   <p class="login-help">首次启动用户名为 admin，一次性随机密码请查看本机启动日志。</p>
  </form>
 </main>
</template>
