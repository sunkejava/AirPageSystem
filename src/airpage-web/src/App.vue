<script setup>
import {computed,onMounted,reactive,ref} from 'vue'
import {api} from './api'
const active=ref('dashboard'),busy=ref(false),toast=ref(''),preview=ref('')
const state=reactive({dashboard:{},templates:[],devices:[],schedules:[],sources:[],history:[]})
const nav=[['dashboard','总览','◫'],['templates','面板模板','▦'],['sources','数据源','⌁'],['schedules','定时推送','◷'],['devices','设备','▣'],['history','推送记录','≡']]
const forms=reactive({
 device:{name:'我的AirPage',deviceUrl:'',isDefault:true},
 schedule:{name:'工作日行情',templateId:'',deviceId:'',cron:'30 9 * * 1-5',timeZoneId:'Asia/Shanghai',enabled:true},
 source:{name:'业务接口',method:'GET',url:'https://api.example.com/status',headersJson:'',body:'',enabled:true},
 template:{name:'自定义状态面板',type:'custom',description:'',dataSourceId:'',schemaJson:'{"titlePath":"$.title","metrics":[{"label":"状态","path":"$.status"}],"itemsPath":"$.items","columns":[{"label":"名称","path":"$.name"},{"label":"值","path":"$.value"}]}'}
})
const defaultDevice=computed(()=>state.devices.find(x=>x.isDefault))
async function load(){const [dashboard,templates,devices,schedules,sources,history]=await Promise.all([api.dashboard(),api.templates(),api.devices(),api.schedules(),api.sources(),api.history()]);Object.assign(state,{dashboard,templates,devices,schedules,sources,history});if(!forms.schedule.templateId&&templates[0])forms.schedule.templateId=templates[0].id;if(!forms.schedule.deviceId&&devices[0])forms.schedule.deviceId=devices[0].id}
async function act(fn,message){busy.value=true;try{await fn();toast.value=message;await load()}catch(e){toast.value=e.message}finally{busy.value=false;setTimeout(()=>toast.value='',3500)}}
async function execute(template,push=true){await act(async()=>{const r=await api.execute({templateId:template.id,deviceId:defaultDevice.value?.id,push});preview.value=r.previewPath},push?'面板已生成并提交推送':'预览已生成')}
onMounted(load)
</script>
<template>
<div class="shell">
 <aside><div class="brand"><span class="mark">A</span><div><b>AirPage</b><small>Snapshot Studio</small></div></div>
  <nav><button v-for="n in nav" :key="n[0]" :class="{active:active===n[0]}" @click="active=n[0]"><i>{{n[2]}}</i>{{n[1]}}</button></nav>
  <div class="device-pill"><span :class="['dot',defaultDevice?'ok':'']"></span><div><small>默认设备</small><b>{{defaultDevice?.name||'尚未配置'}}</b></div></div>
 </aside>
 <main>
  <header><div><h1>{{nav.find(x=>x[0]===active)?.[1]}}</h1><p>构建、调度并推送适合墨水屏的高对比度快照</p></div><button class="ghost" @click="load">刷新数据</button></header>
  <section v-if="active==='dashboard'">
   <div class="stats"><article><span>设备</span><strong>{{state.dashboard.devices||0}}</strong><em>AirPage终端</em></article><article><span>模板</span><strong>{{state.dashboard.templates||0}}</strong><em>可用面板</em></article><article><span>运行任务</span><strong>{{state.dashboard.schedules||0}}</strong><em>Cron调度</em></article><article><span>今日推送</span><strong>{{state.dashboard.pushesToday||0}}</strong><em>执行记录</em></article></div>
   <div class="grid two">
    <article class="card"><div class="card-head"><div><h2>快速推送</h2><p>选择内置或自定义模板立即生成</p></div></div>
     <div class="template-row" v-for="t in state.templates.slice(0,3)" :key="t.id"><span class="template-icon">{{t.type==='market'?'↗':t.type==='server-status'?'◉':'✦'}}</span><div><b>{{t.name}}</b><small>{{t.description}}</small></div><button @click="execute(t,true)" :disabled="busy">推送</button><button class="outline" @click="execute(t,false)" :disabled="busy">预览</button></div>
    </article>
    <article class="card preview"><div class="card-head"><div><h2>最近预览</h2><p>528 × 792 · 2-bit gray4</p></div></div><div class="screen"><img v-if="preview" :src="preview"><div v-else><span>▧</span><p>生成面板后在此预览</p></div></div></article>
   </div>
  </section>
  <section v-if="active==='templates'">
   <div class="grid cards"><article class="card template-card" v-for="t in state.templates" :key="t.id"><span class="tag">{{t.isBuiltIn?'内置':'自定义'}}</span><div class="big-icon">{{t.type==='market'?'↗':t.type==='server-status'?'◉':'✦'}}</div><h2>{{t.name}}</h2><p>{{t.description}}</p><div class="actions"><button @click="execute(t,true)">立即推送</button><button class="outline" @click="execute(t,false)">生成预览</button></div></article></div>
   <article class="card form-card"><h2>新建自定义面板</h2><div class="form-grid"><label>名称<input v-model="forms.template.name"></label><label>数据源<select v-model="forms.template.dataSourceId"><option value="">请选择</option><option v-for="s in state.sources" :value="s.id">{{s.name}}</option></select></label><label class="wide">描述<input v-model="forms.template.description"></label><label class="wide">JSON映射配置<textarea v-model="forms.template.schemaJson" rows="6"></textarea></label></div><button @click="act(()=>api.addTemplate(forms.template),'模板已创建')">保存模板</button></article>
  </section>
  <section v-if="active==='sources'">
   <article class="card form-card"><h2>添加 HTTP JSON 数据源</h2><div class="form-grid"><label>名称<input v-model="forms.source.name"></label><label>方法<select v-model="forms.source.method"><option>GET</option><option>POST</option></select></label><label class="wide">URL<input v-model="forms.source.url"></label><label class="wide">请求头 JSON<textarea v-model="forms.source.headersJson" rows="3" placeholder='{"Authorization":"Bearer ..."}'></textarea></label><label class="wide">请求体<textarea v-model="forms.source.body" rows="3"></textarea></label></div><button @click="act(()=>api.addSource(forms.source),'数据源已保存')">保存数据源</button></article>
   <article class="card table-card"><table><thead><tr><th>名称</th><th>方法</th><th>地址</th><th>状态</th><th></th></tr></thead><tbody><tr v-for="s in state.sources"><td>{{s.name}}</td><td><code>{{s.method}}</code></td><td>{{s.url}}</td><td><span class="status">{{s.enabled?'启用':'停用'}}</span></td><td><button class="outline" @click="act(()=>api.testSource(s.id),'数据源连接正常')">测试</button></td></tr></tbody></table></article>
  </section>
  <section v-if="active==='schedules'">
   <article class="card form-card"><h2>创建定时推送</h2><div class="form-grid"><label>任务名称<input v-model="forms.schedule.name"></label><label>时区<input v-model="forms.schedule.timeZoneId"></label><label>模板<select v-model="forms.schedule.templateId"><option v-for="t in state.templates" :value="t.id">{{t.name}}</option></select></label><label>设备<select v-model="forms.schedule.deviceId"><option v-for="d in state.devices" :value="d.id">{{d.name}}</option></select></label><label class="wide">Cron（5段）<input v-model="forms.schedule.cron" placeholder="30 9 * * 1-5"><small>示例：工作日 9:30 → 30 9 * * 1-5</small></label></div><button @click="act(()=>api.addSchedule(forms.schedule),'定时任务已创建')">创建任务</button></article>
   <article class="card table-card"><table><thead><tr><th>任务</th><th>Cron</th><th>时区</th><th>下次执行</th><th>状态</th><th></th></tr></thead><tbody><tr v-for="s in state.schedules"><td>{{s.name}}</td><td><code>{{s.cron}}</code></td><td>{{s.timeZoneId}}</td><td>{{s.nextRunAt?new Date(s.nextRunAt).toLocaleString():'-'}}</td><td><span :class="['status',!s.enabled&&'off']">{{s.enabled?'运行中':'已停用'}}</span></td><td><button class="outline" @click="act(()=>api.toggleSchedule(s.id),'任务状态已更新')">切换</button></td></tr></tbody></table></article>
  </section>
  <section v-if="active==='devices'">
   <article class="card form-card"><h2>添加 AirPage 设备</h2><p class="hint">设备链接属于凭据，后端仅加密保存设备ID，列表和日志不会回显。</p><div class="form-grid"><label>设备名称<input v-model="forms.device.name"></label><label class="wide">AirPage设备链接<input v-model="forms.device.deviceUrl" type="password" autocomplete="off"></label><label class="check"><input type="checkbox" v-model="forms.device.isDefault">设为默认设备</label></div><button @click="act(()=>api.addDevice(forms.device),'设备已安全添加')">添加设备</button></article>
   <div class="grid cards"><article class="card device-card" v-for="d in state.devices"><span class="tag" v-if="d.isDefault">默认</span><div class="big-icon">▣</div><h2>{{d.name}}</h2><p>{{d.width}} × {{d.height}} · {{d.mode}}</p><small>{{d.origin}}</small><button v-if="!d.isDefault" class="outline" @click="act(()=>api.setDefault(d.id),'默认设备已更新')">设为默认</button></article></div>
  </section>
  <section v-if="active==='history'"><article class="card table-card"><table><thead><tr><th>时间</th><th>状态</th><th>BMP</th><th>耗时</th><th>结果</th><th>预览</th></tr></thead><tbody><tr v-for="h in state.history"><td>{{new Date(h.createdAt).toLocaleString()}}</td><td><span :class="['status',!h.uploadSucceeded&&'off']">{{h.uploadSucceeded?'成功':'未推送'}}</span></td><td>{{(h.bmpBytes/1024).toFixed(1)}} KiB</td><td>{{h.durationMs}} ms</td><td>{{h.message}}</td><td><a v-if="h.previewPath" :href="h.previewPath" target="_blank">查看</a></td></tr></tbody></table></article></section>
 </main>
 <div class="toast" v-if="toast">{{toast}}</div>
</div>
</template>
