async function request(path,options={}){
 const response=await fetch('/api'+path,{credentials:'same-origin',headers:{'Content-Type':'application/json',...(options.headers||{})},...options})
 if(!response.ok)throw new Error(await response.text()||('HTTP '+response.status))
 return response.status===204?null:response.json()
}
export const api={
 login:body=>request('/auth/login',{method:'POST',body:JSON.stringify(body)}),me:()=>request('/auth/me'),logout:()=>request('/auth/logout',{method:'POST'}),changePassword:body=>request('/auth/change-password',{method:'POST',body:JSON.stringify(body)}),
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
 updateTemplate:(id,body)=>request('/templates/'+id,{method:'PUT',body:JSON.stringify(body)}),
 retryPolicies:()=>request('/retry-policies'),addRetryPolicy:body=>request('/retry-policies',{method:'POST',body:JSON.stringify(body)}),updateRetryPolicy:(id,body)=>request('/retry-policies/'+id,{method:'PUT',body:JSON.stringify(body)}),deleteRetryPolicy:id=>request('/retry-policies/'+id,{method:'DELETE'}),
 users:()=>request('/admin/users'),roles:()=>request('/admin/roles'),permissions:()=>request('/admin/permissions'),addUser:body=>request('/admin/users',{method:'POST',body:JSON.stringify(body)}),updateUser:(id,body)=>request('/admin/users/'+id,{method:'PUT',body:JSON.stringify(body)}),addRole:body=>request('/admin/roles',{method:'POST',body:JSON.stringify(body)}),updateRole:(id,body)=>request('/admin/roles/'+id,{method:'PUT',body:JSON.stringify(body)}),deleteRole:id=>request('/admin/roles/'+id,{method:'DELETE'})
}
