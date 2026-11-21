import { Dialog } from './ui';
import { useEffect, useState } from 'react';
import { api, ApiError } from './api';
import { LearningResources } from './learning';
type Focus={requirementKey:string;requirement:string;importance:string;goal:string;practice:string;deliverable:string};
type Plan={pairId:string;weeks:number;hoursPerWeek:number;method:string;careerContext?:string|null;schedule:{week:number;hours:number;focus:Focus[]}[]};
const roadmapContextLabels:Record<string,string>={career_start:'Bắt đầu nghề',career_switch:'Chuyển ngành',promotion:'Thăng tiến'};
export function WeeklyRoadmap({pairId}:{pairId:string}) {
  const [resetOpen,setResetOpen]=useState(false);const [open,setOpen]=useState(false);const [weeks,setWeeks]=useState(4);const [hours,setHours]=useState(5);const [retry,setRetry]=useState(0);
  type Saved={plan:Plan;completed:string[];rowVersion:number;updatedAt:string};
  const [plan,setPlan]=useState<Plan>();const [saved,setSaved]=useState<Saved>();const [loaded,setLoaded]=useState(false);const [busy,setBusy]=useState(false);const [conflict,setConflict]=useState(false);
  const [loading,setLoading]=useState(false);const [error,setError]=useState('');const [message,setMessage]=useState('');
  useEffect(()=>{
    if(!open)return;const c=new AbortController();setLoaded(false);setLoading(true);setError('');setConflict(false);setPlan(undefined);
    void api<{saved:Saved|null}>(`/api/learning/saved-roadmaps/${pairId}`,{signal:c.signal}).then(async reply=>{
      const value=reply.saved;const p=value?.plan??await api<Plan>(`/api/learning/roadmap/${pairId}?weeks=4&hoursPerWeek=5`,{signal:c.signal});
      if(c.signal.aborted)return;setSaved(value??undefined);setWeeks(p.weeks);setHours(p.hoursPerWeek);setPlan(p);setLoaded(true);
    }).catch(e=>{if(!c.signal.aborted)setError((e as Error).message)}).finally(()=>{if(!c.signal.aborted)setLoading(false)});
    return()=>c.abort();
  },[open,pairId,retry]);
  useEffect(()=>{
    if(!open||!loaded)return;const c=new AbortController();setLoading(true);setPlan(undefined);
    void api<Plan>(`/api/learning/roadmap/${pairId}?weeks=${weeks}&hoursPerWeek=${hours}`,{signal:c.signal}).then(p=>{if(!c.signal.aborted)setPlan(saved&&saved.plan.weeks===weeks&&saved.plan.hoursPerWeek===hours?saved.plan:p)}).catch(e=>{if(!c.signal.aborted)setError((e as Error).message)}).finally(()=>{if(!c.signal.aborted)setLoading(false)});return()=>c.abort();
  },[open,loaded,pairId,weeks,hours,saved]);
  const changed=!!saved&&(saved.plan.weeks!==weeks||saved.plan.hoursPerWeek!==hours);
  async function write(week?:number,key?:string,completed?:boolean,confirmed=false){
    if(changed&&week===undefined&&!confirmed){setResetOpen(true);return;}setResetOpen(false);
    setBusy(true);setError('');setMessage('');
    try{const value=await api<Saved>(`/api/learning/saved-roadmaps/${pairId}${week===undefined?'':'/progress'}`,{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(week===undefined?{weeks,hoursPerWeek:hours,rowVersion:saved?.rowVersion??0}:{week,requirementKey:key,completed,rowVersion:saved?.rowVersion})});setSaved(value);setPlan(value.plan);setMessage(week===undefined?'Đã lưu lộ trình.':'Đã lưu tiến độ.');}
    catch(e){setError((e as Error).message);if(e instanceof ApiError&&e.status===409)setConflict(true)}finally{setBusy(false)}
  }
  return <section className="weekly-roadmap" aria-label="Lộ trình học theo tuần"><h3>Lộ trình học theo tuần</h3><p>Ưu tiên yêu cầu bắt buộc chưa có đủ bằng chứng. Đây là kế hoạch gợi ý theo kết quả đã lưu; bạn cần điều chỉnh theo nền tảng và thời gian thực tế.</p>
    <button className="secondary" aria-expanded={open} onClick={()=>setOpen(v=>!v)}>{open?'Thu gọn lộ trình':'Xem lộ trình học theo tuần'}</button>
    {open&&<><div className="row roadmap-options"><label>Số tuần<select disabled={busy||!loaded||conflict} value={weeks} onChange={e=>setWeeks(Number(e.target.value))}>{[2,4,6,8,12].map(n=><option key={n} value={n}>{n} tuần</option>)}</select></label><label>Giờ học mỗi tuần<select disabled={busy||!loaded||conflict} value={hours} onChange={e=>setHours(Number(e.target.value))}>{[1,3,5,10,15,20].map(n=><option key={n} value={n}>{n} giờ</option>)}</select></label></div>
      <button className="secondary" disabled={busy||loading||!plan?.schedule.length||conflict} onClick={()=>void write()}>{busy?'Đang lưu…':changed?'Lưu thay đổi và đặt lại tiến độ':saved?'Lưu lộ trình':'Lưu lộ trình để theo dõi'}</button>
      {saved&&!changed&&<p role="status">Tiến độ: {saved.completed.length}/{saved.plan.schedule.reduce((n,w)=>n+w.focus.length,0)} mục tiêu hoàn thành.</p>}
      {changed&&<p role="status">Bản xem trước chưa được lưu; tiến độ cũ được giữ cho đến khi bạn xác nhận thay đổi.</p>}
      {message&&<p role="status">{message}</p>}
      <button className="secondary" disabled={busy||loading} onClick={()=>setRetry(v=>v+1)}>Tải lại lộ trình đã lưu</button>
      {loading&&<p role="status">Đang sắp xếp lộ trình…</p>}{error&&<p role="alert">{error} <button className="secondary" onClick={()=>setRetry(v=>v+1)}>Thử lại lộ trình</button></p>}
      {plan&&plan.pairId===pairId&&<>{!plan.schedule.length?<p>Không có tiêu chí thiếu hoặc mới đáp ứng một phần để lập lộ trình.</p>:<><p role="status">{plan.weeks} tuần · {plan.hoursPerWeek} giờ/tuần · tổng {plan.weeks*plan.hoursPerWeek} giờ dự kiến{plan.careerContext?` · Bối cảnh: ${roadmapContextLabels[plan.careerContext]??plan.careerContext}`:''}.</p><p className="hint">Chia tổng giờ của tuần cho các mục tiêu trong tuần đó. Hoàn thành bài tập không tự thay đổi điểm đối chiếu hoặc chứng minh đã đáp ứng yêu cầu JD.</p>
        <ol className="roadmap-weeks">{plan.schedule.map(week=><li key={week.week}><h4>Tuần {week.week} · {week.hours} giờ</h4>{week.focus.map(f=><article key={f.requirementKey}><h5>{f.requirement}</h5><p>{f.importance==='required'?'Yêu cầu bắt buộc':'Yêu cầu ưu tiên / chưa phân loại'}</p><p><strong>Mục tiêu:</strong> {f.goal}</p><p><strong>Thực hành:</strong> {f.practice}</p><p><strong>Sản phẩm đầu ra:</strong> {f.deliverable}</p>{saved&&!changed&&<label><input type="checkbox" disabled={busy||loading||conflict} checked={saved.completed.includes(week.week+':'+f.requirementKey)} onChange={e=>void write(week.week,f.requirementKey,e.target.checked)}/>Đã hoàn thành mục tiêu tuần {week.week}</label>}<LearningResources pairId={pairId} requirementKey={f.requirementKey}/></article>)}</li>)}</ol></>}</>}
    </>}
    <Dialog open={resetOpen} title="Đặt lại tiến độ lộ trình" busy={busy} onClose={()=>setResetOpen(false)}><p>Thay đổi thời lượng sẽ đặt lại toàn bộ tiến độ của lộ trình này.</p><button className="secondary" disabled={busy} onClick={()=>setResetOpen(false)}>Hủy thay đổi</button><button disabled={busy} onClick={()=>void write(undefined,undefined,undefined,true)}>Xác nhận lưu và đặt lại</button></Dialog>
  </section>;
}
