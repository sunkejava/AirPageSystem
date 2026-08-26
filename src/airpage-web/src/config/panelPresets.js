export const panelPresets=Object.freeze({
 quote:['每日金句',{preset:'quote',title:'每日金句',quote:'保持专注，把复杂的事做简单。',author:'佚名'}],
 badge:['电子工牌',{preset:'badge',company:'AIRPAGE SYSTEM',name:'张三',role:'高级工程师',id:'AP-0001',department:'研发中心',message:'专业 · 可靠 · 高效'}],
 boarding:['电子登机牌',{preset:'boarding-pass',airline:'AIRPAGE AIR',name:'ZHANG SAN',from:'SHA',to:'PEK',route:'上海虹桥 → 北京首都',flight:'AP1024',gate:'A12',seat:'08A',boarding:'08:30'}],
 layout:['自由绘制',{title:'自定义面板',footer:'JSON绘制DSL',elements:[{type:'text',text:'Hello AirPage',x:30,y:130,width:460,size:36,bold:true},{type:'box',x:30,y:210,width:468,height:180,stroke:3}]}]
})
