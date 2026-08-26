async function request(path,options={}){
 const response=await fetch('/api'+path,{headers:{'Content-Type':'application/json',...(options.headers||{})},...options})
 if(!response.ok)throw new Error(await response.text()||('HTTP '+response.status))
 return response.status===204?null:response.json()
}
export const api={
 dashboard:()=>request('/dashboard'),templates:()=>request('/templates'),devices:()=>request('/devices'),
 schedules:()=>request('/schedules'),sources:()=>request('/data-sources'),history:()=>request('/history'),
 execute:body=>request('/panels/execute',{method:'POST',body:JSON.stringify(body)}),
 addDevice:body=>request('/devices',{method:'POST',body:JSON.stringify(body)}),
 setDefault:id=>request('/devices/'+id+'/default',{method:'PUT'}),
 addSchedule:body=>request('/schedules',{method:'POST',body:JSON.stringify(body)}),
 updateSchedule:(id,body)=>request('/schedules/'+id,{method:'PUT',body:JSON.stringify(body)}),
 deleteSchedule:id=>request('/schedules/'+id,{method:'DELETE'}),
 runSchedule:id=>request('/schedules/'+id+'/run',{method:'POST'}),
 toggleSchedule:id=>request('/schedules/'+id+'/toggle',{method:'PUT'}),
 addSource:body=>request('/data-sources',{method:'POST',body:JSON.stringify(body)}),
 testSource:id=>request('/data-sources/'+id+'/test',{method:'POST'}),
 addTemplate:body=>request('/templates',{method:'POST',body:JSON.stringify(body)}),
 updateTemplate:(id,body)=>request('/templates/'+id,{method:'PUT',body:JSON.stringify(body)})
}
