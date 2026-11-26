import { useEffect, useRef, useState } from 'react';
import { api, ApiError } from './api';
import { PairDetails, type Pair } from './pairMatching';
import { useAiJobPolling } from './useAiJobPolling';
import { Alert, ConfirmDialog, EmptyState, PageHeader, Pagination, StaticLoading } from './ui';

type Entry={id:string;createdAt:string;status:string;context:string;available:boolean;jobTitle:string|null;resumeVersionNo:number|null;score:number|null};
type Page={items:Entry[];total:number;page:number;pageSize:number};
const statuses:Record<string,string>={queued:'Đang chờ',running:'Đang phân tích',succeeded:'Thành công',failed:'Thất bại',cancelled:'Đã hủy'};
const contexts:Record<string,string>={self:'CV của tôi với việc làm',public_pool:'CV công khai',application:'Hồ sơ ứng tuyển'};
export function AnalysisHistory({active}:{active:boolean}) {
  const [hash,setHash]=useState(window.location.hash);const [listRetry,setListRetry]=useState(0);const [detailRetry,setDetailRetry]=useState(0);
  const [data,setData]=useState<Page>();const [error,setError]=useState('');const [loading,setLoading]=useState(false);
  const [pair,setPair]=useState<Pair>();const [detailError,setDetailError]=useState('');const [missing,setMissing]=useState(false);const [detailLoading,setDetailLoading]=useState(false);
  const [hidingId,setHidingId]=useState<string>();
  const [confirmHideId,setConfirmHideId]=useState<string>();
  const loadedDetailId=useRef<string|undefined>(undefined);
  async function hideEntry(targetId:string){
    setHidingId(targetId);setError('');setDetailError('');
    try{
      await api(`/api/matching/history/${targetId}`,{method:'DELETE'});
      setConfirmHideId(undefined);
      if(id===targetId){window.location.hash=href({id:null});}
      setListRetry(n=>n+1);
    }catch(e){if(id===targetId)setDetailError((e as Error).message);else setError((e as Error).message);}
    finally{setHidingId(undefined);}
  }
  useEffect(()=>{const update=()=>setHash(window.location.hash);window.addEventListener('hashchange',update);return()=>window.removeEventListener('hashchange',update)},[]);
  const params=new URLSearchParams(hash.split('?')[1]??'');const status=params.get('status')??'all',context=params.get('context')??'all';
  const rawPage=Number(params.get('page')??1);const page=Number.isInteger(rawPage)&&rawPage>=1&&rawPage<=10000?rawPage:1;const id=params.get('id');
  const validId=!id||/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(id);
  function href(changes:Record<string,string|null>){const next=new URLSearchParams({status,context,page:String(page)});if(id)next.set('id',id);for(const [k,v] of Object.entries(changes)){if(v===null)next.delete(k);else next.set(k,v);}return '#analysis-history?'+next.toString();}
  useEffect(()=>{
    if(!active||id)return;
    const controller=new AbortController();setLoading(true);setData(undefined);setError('');
    void api<Page>(`/api/matching/history?${new URLSearchParams({status,context,page:String(page)})}`,{signal:controller.signal}).then(x=>{if(!controller.signal.aborted)setData(x)}).catch(e=>{if(!controller.signal.aborted)setError((e as Error).message)}).finally(()=>{if(!controller.signal.aborted)setLoading(false)});
    return()=>controller.abort();
  },[active,id,status,context,page,listRetry]);
  useEffect(()=>{
    loadedDetailId.current=undefined;
    setPair(undefined);setDetailError('');setMissing(false);setDetailLoading(!!(id&&validId));
  },[id,validId,detailRetry]);
  const { timedOut, retryPolling } = useAiJobPolling({
    pollKey: id && validId ? `${id}:${detailRetry}` : undefined,
    active,
    maxAttempts: 60,
    intervalMs: 2500,
    tick: async signal => {
      if (loadedDetailId.current !== id) setDetailLoading(true);
      try {
        const x = await api<Pair>(`/api/matching/pairs/${id}`, { signal });
        if (signal.aborted) return false;
        loadedDetailId.current = id!;
        setPair(x);
        setDetailLoading(false);
        return x.status !== 'queued' && x.status !== 'running';
      } catch (e) {
        if (signal.aborted) return false;
        setDetailLoading(false);
        if (e instanceof ApiError && (e.status === 403 || e.status === 404)) {
          setMissing(true);
          return true;
        }
        throw e;
      }
    },
    onError: e => { setDetailLoading(false); setDetailError((e as Error).message); },
  });
  return <section className="card analysis-history" aria-label="Lịch sử phân tích">
    <PageHeader title="Lịch sử phân tích" description="Các lượt đối chiếu CV–JD do bạn yêu cầu, kể cả lượt chưa hoàn thành. Xem lại không tạo phân tích mới và không sử dụng thêm ngân sách AI."/>
    {id?<section aria-label="Kết quả phân tích đã lưu"><a href={href({id:null})}>← Quay lại danh sách lịch sử</a><h2>Kết quả của lượt đã chọn</h2><p className="hint">Kết quả theo dữ liệu và phương pháp tại lần phân tích đó; không tự thay bằng lượt mới nhất.</p>
      {(!validId||missing)&&<Alert tone="warning">Kết quả không còn khả dụng hoặc bạn không có quyền truy cập.</Alert>}
      {detailLoading&&<StaticLoading message="Đang mở kết quả đã lưu…"/>}
      {detailError&&<Alert tone="error" actions={timedOut&&<button type="button" className="secondary" onClick={()=>{setDetailError('');retryPolling();}}>Kiểm tra lại kết quả</button>}>{detailError}</Alert>}
      {pair&&pair.id===id&&<><p>{contexts[pair.context]} · {statuses[pair.status]}</p><PairDetails pair={pair}/></>}
      <div className="row"><button type="button" className="secondary" disabled={detailLoading} onClick={()=>setDetailRetry(n=>n+1)}>Tải lại kết quả</button>{pair&&!['queued','running'].includes(pair.status)&&<button type="button" className="secondary" disabled={hidingId===pair.id} onClick={()=>setConfirmHideId(pair.id)}>{hidingId===pair.id?'Đang ẩn…':'Ẩn khỏi lịch sử'}</button>}</div>
    </section>:<><div className="row history-filters"><label>Trạng thái phân tích<select value={status} onChange={e=>{window.location.hash=href({status:e.target.value,page:'1',id:null})}}><option value="all">Tất cả trạng thái</option>{Object.entries(statuses).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label>
      <label>Ngữ cảnh phân tích<select value={context} onChange={e=>{window.location.hash=href({context:e.target.value,page:'1',id:null})}}><option value="all">Tất cả ngữ cảnh</option>{Object.entries(contexts).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label><button type="button" className="secondary" disabled={loading} onClick={()=>setListRetry(n=>n+1)}>Tải lại lịch sử</button></div>
      {loading&&<StaticLoading message="Đang tải lịch sử…"/>}
      {error&&<Alert tone="error" actions={<button type="button" className="secondary" onClick={()=>setListRetry(n=>n+1)}>Thử lại lịch sử</button>}>{error}</Alert>}
      {data&&<><p role="status">{data.total} lượt phân tích · {data.items.length?(page-1)*data.pageSize+1:0}–{data.items.length?Math.min(page*data.pageSize,data.total):0}</p>
        {!data.items.length&&<EmptyState title="Không có lượt phân tích trong trang hoặc bộ lọc này."/>}
        <ul className="application-list">{data.items.map(row=><li key={row.id} data-analysis-id={row.id}><div><h3>{row.available?row.jobTitle:'Nội dung không còn khả dụng'}</h3><p>{new Date(row.createdAt).toLocaleString('vi-VN')} · {statuses[row.status]} · {contexts[row.context]}</p>
          {row.available?<><p>CV phiên bản {row.resumeVersionNo} · {row.score===null?'Chưa có điểm':`Điểm đối chiếu ${row.score.toFixed(2)}/100`}</p><div className="row"><a href={href({id:row.id})}>Mở lượt phân tích đã lưu</a>{!['queued','running'].includes(row.status)&&<button type="button" className="secondary" disabled={hidingId===row.id} onClick={()=>setConfirmHideId(row.id)}>{hidingId===row.id?'Đang ẩn…':'Ẩn khỏi lịch sử'}</button>}</div></>:<><p>Quyền hoặc trạng thái nguồn đã thay đổi. Nội dung và điểm không được hiển thị.</p>{!['queued','running'].includes(row.status)&&<button type="button" className="secondary" disabled={hidingId===row.id} onClick={()=>setConfirmHideId(row.id)}>{hidingId===row.id?'Đang ẩn…':'Ẩn khỏi lịch sử'}</button>}</>}</div></li>)}</ul>
        <Pagination page={page} totalItems={data.total} pageSize={data.pageSize} busy={loading} ariaLabel="Phân trang lịch sử" onPageChange={next=>{window.location.hash=href({page:String(next)})}}/></>}
    </>}
    <ConfirmDialog
      open={!!confirmHideId}
      title="Ẩn khỏi lịch sử phân tích"
      message="Ẩn lượt phân tích này khỏi lịch sử cá nhân?"
      confirmLabel="Ẩn khỏi lịch sử"
      busy={!!hidingId}
      onClose={()=>setConfirmHideId(undefined)}
      onConfirm={async ()=>{if(confirmHideId)await hideEntry(confirmHideId);}}
    />
  </section>;
}
