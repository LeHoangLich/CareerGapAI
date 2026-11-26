import {useCallback,useEffect,useRef,useState} from 'react';
import {useRecruitmentLive} from './recruitmentLive';
import {Bell} from '@phosphor-icons/react';
import {api,ApiError} from './api';
import {StaticLoading} from './workspace';
import {jobHref} from './jobLinks';
import {applicationHref,applicationIdFromHash} from './notificationLinks';

type Notice={id:string;notificationType:string;applicationId:string|null;title:string;body:string;createdAt:string;readAt:string|null;href:string|null};
type Inbox={items:Notice[];page:number;pageSize:number;total:number;unread:number};
const types:Record<string,string>={application_submitted:'Hồ sơ mới',application_status_changed:'Cập nhật trạng thái',system_notice:'Thông báo khác'};
const statuses:Record<string,string>={submitted:'Đã nộp',reviewing:'Đang xem xét',shortlisted:'Được chọn tiếp',rejected:'Không tiếp tục',withdrawn:'Đã rút'};

export function RecruitmentNotifications({active,revision,userId}:{active:boolean;revision:number;userId?:string}) {
  const live=useRecruitmentLive(userId);
  const [query,setQuery]=useState({page:1,read:'all',type:''});
  const [inbox,setInbox]=useState<Inbox>();
  const [loading,setLoading]=useState(false),[error,setError]=useState('');
  const [mutating,setMutating]=useState(false);
  const request=useRef(0);
  const load=useCallback(async(signal?:AbortSignal)=>{
    const ticket=++request.current;setLoading(true);setError('');
    try{
      const params=new URLSearchParams({page:String(query.page),pageSize:'10',read:query.read});
      if(query.type)params.set('type',query.type);
      const result=await api<Inbox>(`/api/notifications/inbox?${params}`,{signal});
      if(signal?.aborted||ticket!==request.current)return;
      const last=Math.max(1,Math.ceil(result.total/result.pageSize));
      if(query.page>last){setQuery(current=>({...current,page:last}));return;}
      setInbox(result);
    }catch(e){if(!signal?.aborted&&ticket===request.current){setInbox(undefined);setError((e as Error).message);}}
    finally{if(ticket===request.current)setLoading(false);}
  },[query]);
  useEffect(()=>{
    if(!active)return;
    const controller=new AbortController();void load(controller.signal);
    return()=>{request.current++;controller.abort();};
  },[active,load,revision,live.revision]);
  useEffect(()=>{
    if(!active)return;let controller:AbortController|undefined;
    const refresh=()=>{controller?.abort();controller=new AbortController();void load(controller.signal)};
    window.addEventListener('focus',refresh);window.addEventListener('online',refresh);
    return()=>{controller?.abort();window.removeEventListener('focus',refresh);window.removeEventListener('online',refresh)};
  },[active,load]);
  async function mark(id:string,target?:string){
    setMutating(true);setError('');
    try{
      await api(id==='all'?'/api/notifications/read-all':`/api/notifications/${id}/read`,{method:'PUT'});
      if(target)window.location.hash=target;else await load();
    }catch(e){setError((e as Error).message);}
    finally{setMutating(false);}
  }
  const paging=(position:string)=><nav className="row" aria-label={`Phân trang thông báo phía ${position}`}>
    <button type="button" className="secondary" disabled={loading||mutating||query.page<=1} onClick={()=>setQuery(current=>({...current,page:current.page-1}))}>Trang thông báo trước</button>
    <span>Trang {query.page} / {inbox?Math.max(1,Math.ceil(inbox.total/inbox.pageSize)):'…'}</span>
    <button type="button" className="secondary" disabled={loading||mutating||!inbox||query.page*inbox.pageSize>=inbox.total} onClick={()=>setQuery(current=>({...current,page:current.page+1}))}>Trang thông báo sau</button>
  </nav>;
  return <section className="card application-center notification-center" aria-label="Thông báo tuyển dụng" aria-busy={loading}>
    <div className="section-heading"><div><p className="eyebrow">CẬP NHẬT TUYỂN DỤNG</p><h1>Thông báo tuyển dụng</h1></div><span>{inbox?`${inbox.unread} chưa đọc trong toàn bộ hộp thư`:loading?'Đang tải…':'Chưa tải được'}</span></div>
    <p className="hint" role="status">{live.connection==='connected'?'Đang cập nhật trực tiếp. Bộ lọc và trang hiện tại được giữ.':live.connection==='reconnecting'?'Đang kết nối lại cập nhật trực tiếp. Bạn vẫn có thể tải lại thông báo.':'Đang kết nối cập nhật trực tiếp…'}</p>
    <div className="row notification-filters">
      <label>Trạng thái thông báo<select value={query.read} disabled={mutating} onChange={e=>setQuery(current=>({...current,read:e.target.value,page:1}))}><option value="all">Tất cả</option><option value="unread">Chưa đọc</option><option value="read">Đã đọc</option></select></label>
      <label>Loại thông báo<select value={query.type} disabled={mutating} onChange={e=>setQuery(current=>({...current,type:e.target.value,page:1}))}><option value="">Tất cả loại</option>{Object.entries(types).map(([value,label])=><option key={value} value={value}>{label}</option>)}</select></label>
    </div>
    <div className="row"><button type="button" className="secondary" disabled={loading||mutating} onClick={()=>void load()}>Tải lại thông báo</button><button type="button" disabled={loading||mutating||!inbox?.unread} onClick={()=>void mark('all')}>Đọc tất cả thông báo</button></div>
    <p className="hint">“Đọc tất cả thông báo” áp dụng cho mọi trang và mọi bộ lọc trong hộp thư của bạn.</p>
    {error&&<p role="alert" className="error">{error}</p>}
    <p role="status">{loading?'Đang tải thông báo…':inbox?`${inbox.total} thông báo · ${inbox.items.length?(inbox.page-1)*inbox.pageSize+1:0}–${(inbox.page-1)*inbox.pageSize+inbox.items.length}`:''}</p>
    {paging('trên')}
    {!loading&&inbox&&!inbox.items.length&&<div className="empty-state notification-empty"><Bell size={28} aria-hidden="true"/><h3>{query.read!=='all'||query.type?'Không có thông báo khớp bộ lọc.':'Chưa có thông báo tuyển dụng.'}</h3></div>}
    <ul className="application-list notification-list">{inbox?.items.map(n=>{
      const id=applicationIdFromHash(n.href??'');const href=id?applicationHref(id):null;
      return <li key={n.id} data-notification-id={n.id} className={n.readAt?'notification-read':'notification-unread'}><div><strong>{n.title}</strong><p>{n.body}</p><small>{types[n.notificationType]??'Thông báo khác'} · {new Date(n.createdAt).toLocaleString('vi-VN')} · {n.readAt?'Đã đọc':'Chưa đọc'}</small>
        <div className="row">{href&&<a href={href} aria-disabled={mutating} onClick={event=>{if(mutating){event.preventDefault();return;}if(!n.readAt&&!event.ctrlKey&&!event.metaKey&&!event.shiftKey){event.preventDefault();void mark(n.id,href);}}}>Xem hồ sơ ứng tuyển</a>}
        {!n.readAt&&<button type="button" className="secondary" disabled={mutating||loading} onClick={()=>void mark(n.id)}>Đánh dấu đã đọc</button>}</div>
      </div></li>;
    })}</ul>
    {paging('dưới')}
  </section>;
}

type Application={id:string;jobId:string;jobTitle:string;companyName:string;status:string;submittedAt:string;resumeVersionNo:number;coverLetter:string|null};
type History={fromStatus:string|null;toStatus:string;changedAt:string};
export function NotificationApplicationDetail({active}:{active:boolean}) {
  const [id,setId]=useState(()=>applicationIdFromHash(window.location.hash));
  const [retry,setRetry]=useState(0);
  const [data,setData]=useState<{id:string;application?:Application;history?:History[];error?:string;missing?:boolean}>();
  useEffect(()=>{const update=()=>{if(window.location.hash!=='#main-content')setId(applicationIdFromHash(window.location.hash));};window.addEventListener('hashchange',update);return()=>window.removeEventListener('hashchange',update);},[]);
  useEffect(()=>{
    if(!active||!id)return;
    const controller=new AbortController();setData(undefined);
    void Promise.all([api<Application>(`/api/applications/${id}`,{signal:controller.signal}),api<History[]>(`/api/applications/${id}/history`,{signal:controller.signal})])
      .then(([application,history])=>{if(!controller.signal.aborted)setData({id,application,history});})
      .catch(e=>{if(!controller.signal.aborted)setData({id,error:(e as Error).message,missing:e instanceof ApiError&&[403,404].includes(e.status)});});
    return()=>controller.abort();
  },[active,id,retry]);
  const current=data?.id===id?data:undefined;
  const application=current?.application;
  return <section className="card notification-application" aria-label="Chi tiết hồ sơ ứng tuyển">
    <a href="#notifications">← Quay lại thông báo</a>
    {!id||current?.missing?<><h1>Hồ sơ không còn khả dụng</h1><p>Hồ sơ không tồn tại, đã hết thời hạn lưu hoặc bạn không còn quyền xem.</p></>:current?.error?<div role="alert"><h1>Chưa tải được hồ sơ</h1><p>{current.error}</p><button type="button" className="secondary" onClick={()=>setRetry(value=>value+1)}>Tải lại hồ sơ liên quan</button></div>:!application?<StaticLoading label="Đang tải hồ sơ liên quan…"/>:<>
      <p className="eyebrow">CHI TIẾT HỒ SƠ ỨNG TUYỂN</p><h1>{application.jobTitle}</h1><p>{application.companyName}</p>
      <p><strong>{statuses[application.status]??application.status}</strong> · CV phiên bản {application.resumeVersionNo} · Nộp lúc {new Date(application.submittedAt).toLocaleString('vi-VN')}</p>
      <div className="row"><a href={`/api/applications/${application.id}/resume`}>Mở CV đã nộp</a><a href={jobHref(application.jobId)}>Xem tin tuyển dụng</a></div>
      <h2>Thư ứng tuyển đã gửi</h2>{application.coverLetter?<p className="submitted-letter-text">{application.coverLetter}</p>:<p>Không gửi thư ứng tuyển.</p>}
      <h2>Lịch sử trạng thái</h2><ul className="status-history">{current?.history?.map((entry,index)=><li key={`${entry.changedAt}-${index}`}><span>{entry.fromStatus?`${statuses[entry.fromStatus]??entry.fromStatus} → `:''}{statuses[entry.toStatus]??entry.toStatus}</span><small>{new Date(entry.changedAt).toLocaleString('vi-VN')}</small></li>)}</ul>
    </>}
  </section>;
}
