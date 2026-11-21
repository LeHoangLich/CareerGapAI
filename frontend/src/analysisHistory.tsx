import { useEffect, useState } from 'react';
import { api, ApiError } from './api';
import { PairDetails, type Pair } from './pairMatching';

type Entry={id:string;createdAt:string;status:string;context:string;available:boolean;jobTitle:string|null;resumeVersionNo:number|null;score:number|null};
type Page={items:Entry[];total:number;page:number;pageSize:number};
const statuses:Record<string,string>={queued:'Đang chờ',running:'Đang phân tích',succeeded:'Thành công',failed:'Thất bại',cancelled:'Đã hủy'};
const contexts:Record<string,string>={self:'CV của tôi với việc làm',public_pool:'CV công khai',application:'Hồ sơ ứng tuyển'};
export function AnalysisHistory({active}:{active:boolean}) {
  const [hash,setHash]=useState(window.location.hash);const [retry,setRetry]=useState(0);
  const [data,setData]=useState<Page>();const [error,setError]=useState('');const [loading,setLoading]=useState(false);
  const [pair,setPair]=useState<Pair>();const [detailError,setDetailError]=useState('');const [missing,setMissing]=useState(false);const [detailLoading,setDetailLoading]=useState(false);
  const [hidingId,setHidingId]=useState<string>();
  async function hideEntry(targetId:string){
    if(!confirm('Ẩn lượt phân tích này khỏi lịch sử cá nhân?'))return;
    setHidingId(targetId);setError('');setDetailError('');
    try{
      await api(`/api/matching/history/${targetId}`,{method:'DELETE'});
      if(id===targetId){window.location.hash=href({id:null});}
      setRetry(n=>n+1);
    }catch(e){if(id===targetId)setDetailError((e as Error).message);else setError((e as Error).message);}
    finally{setHidingId(undefined);}
  }
  useEffect(()=>{const update=()=>setHash(window.location.hash);window.addEventListener('hashchange',update);return()=>window.removeEventListener('hashchange',update)},[]);
  const params=new URLSearchParams(hash.split('?')[1]??'');const status=params.get('status')??'all',context=params.get('context')??'all';
  const rawPage=Number(params.get('page')??1);const page=Number.isInteger(rawPage)&&rawPage>=1&&rawPage<=10000?rawPage:1;const id=params.get('id');
  const validId=!id||/^[0-9a-f]{8}(-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(id);
  function href(changes:Record<string,string|null>){const next=new URLSearchParams({status,context,page:String(page)});if(id)next.set('id',id);for(const [k,v] of Object.entries(changes)){if(v===null)next.delete(k);else next.set(k,v);}return '#analysis-history?'+next.toString();}
  useEffect(()=>{
    if(!active)return;const controller=new AbortController();setLoading(true);setData(undefined);setError('');
    void api<Page>(`/api/matching/history?${new URLSearchParams({status,context,page:String(page)})}`,{signal:controller.signal}).then(x=>{if(!controller.signal.aborted)setData(x)}).catch(e=>{if(!controller.signal.aborted)setError((e as Error).message)}).finally(()=>{if(!controller.signal.aborted)setLoading(false)});
    return()=>controller.abort();
  },[active,status,context,page,retry]);
  useEffect(()=>{
    setPair(undefined);setDetailError('');setMissing(false);setDetailLoading(false);
    if(!active||!id||!validId)return;const controller=new AbortController();setDetailLoading(true);
    void api<Pair>(`/api/matching/pairs/${id}`,{signal:controller.signal}).then(x=>{if(!controller.signal.aborted)setPair(x)}).catch(e=>{if(!controller.signal.aborted){if(e instanceof ApiError&&(e.status===403||e.status===404))setMissing(true);else setDetailError((e as Error).message)}}).finally(()=>{if(!controller.signal.aborted)setDetailLoading(false)});
    return()=>controller.abort();
  },[active,id,validId,retry]);
  return <section className="card analysis-history" aria-label="Lịch sử phân tích"><h1>Lịch sử phân tích</h1><p>Các lượt đối chiếu CV–JD do bạn yêu cầu, kể cả lượt chưa hoàn thành. Xem lại không tạo phân tích mới và không sử dụng thêm ngân sách AI.</p>
    {id?<section aria-label="Kết quả phân tích đã lưu"><a href={href({id:null})}>← Quay lại danh sách lịch sử</a><h2>Kết quả của lượt đã chọn</h2><p className="hint">Kết quả theo dữ liệu và phương pháp tại lần phân tích đó; không tự thay bằng lượt mới nhất.</p>
      {(!validId||missing)&&<p role="status">Kết quả không còn khả dụng hoặc bạn không có quyền truy cập.</p>}{detailLoading&&<p role="status">Đang mở kết quả đã lưu…</p>}{detailError&&<p role="alert">{detailError}</p>}
      {pair&&pair.id===id&&<><p>{contexts[pair.context]} · {statuses[pair.status]}</p><PairDetails pair={pair}/></>}
      <div className="row"><button className="secondary" disabled={detailLoading} onClick={()=>setRetry(n=>n+1)}>Tải lại kết quả</button>{pair&&!['queued','running'].includes(pair.status)&&<button className="secondary" disabled={hidingId===pair.id} onClick={()=>void hideEntry(pair.id)}>{hidingId===pair.id?'Đang ẩn…':'Ẩn khỏi lịch sử'}</button>}</div>
    </section>:<><div className="row history-filters"><label>Trạng thái phân tích<select value={status} onChange={e=>{window.location.hash=href({status:e.target.value,page:'1',id:null})}}><option value="all">Tất cả trạng thái</option>{Object.entries(statuses).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label>
      <label>Ngữ cảnh phân tích<select value={context} onChange={e=>{window.location.hash=href({context:e.target.value,page:'1',id:null})}}><option value="all">Tất cả ngữ cảnh</option>{Object.entries(contexts).map(([k,v])=><option key={k} value={k}>{v}</option>)}</select></label><button className="secondary" disabled={loading} onClick={()=>setRetry(n=>n+1)}>Tải lại lịch sử</button></div>
      {loading&&<p role="status">Đang tải lịch sử…</p>}{error&&<p role="alert">{error} <button onClick={()=>setRetry(n=>n+1)}>Thử lại lịch sử</button></p>}
      {data&&<><p role="status">{data.total} lượt phân tích · {data.items.length?(page-1)*data.pageSize+1:0}–{data.items.length?Math.min(page*data.pageSize,data.total):0}</p>
        {!data.items.length&&<p>Không có lượt phân tích trong trang hoặc bộ lọc này.</p>}<ul className="application-list">{data.items.map(row=><li key={row.id} data-analysis-id={row.id}><div><h3>{row.available?row.jobTitle:'Nội dung không còn khả dụng'}</h3><p>{new Date(row.createdAt).toLocaleString('vi-VN')} · {statuses[row.status]} · {contexts[row.context]}</p>
          {row.available?<><p>CV phiên bản {row.resumeVersionNo} · {row.score===null?'Chưa có điểm':`Điểm đối chiếu ${row.score.toFixed(2)}/100`}</p><div className="row"><a href={href({id:row.id})}>Mở lượt phân tích đã lưu</a>{!['queued','running'].includes(row.status)&&<button type="button" className="secondary" disabled={hidingId===row.id} onClick={()=>void hideEntry(row.id)}>{hidingId===row.id?'Đang ẩn…':'Ẩn khỏi lịch sử'}</button>}</div></>:<><p>Quyền hoặc trạng thái nguồn đã thay đổi. Nội dung và điểm không được hiển thị.</p>{!['queued','running'].includes(row.status)&&<button type="button" className="secondary" disabled={hidingId===row.id} onClick={()=>void hideEntry(row.id)}>{hidingId===row.id?'Đang ẩn…':'Ẩn khỏi lịch sử'}</button>}</>}</div></li>)}</ul>
        <nav className="row" aria-label="Phân trang lịch sử"><button className="secondary" disabled={loading||page===1} onClick={()=>{window.location.hash=href({page:String(page-1)})}}>Trang lịch sử trước</button><span>Trang {page} / {Math.max(1,Math.ceil(data.total/data.pageSize))}</span><button className="secondary" disabled={loading||page*data.pageSize>=data.total} onClick={()=>{window.location.hash=href({page:String(page+1)})}}>Trang lịch sử sau</button></nav></>}
    </>}
  </section>;
}
