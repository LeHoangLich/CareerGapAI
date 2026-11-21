import { useEffect, useState } from 'react';
import { api, ApiError } from './api';
import { Dialog, ValidatedForm } from './ui';

type Report = { id:string; targetType:string; targetId:string|null; reason:string; status:string; rowVersion:number; createdAt:string; title?:string|null };
type ReportPage = {items:Report[];total:number;page:number;pageSize:number};
type Detail = { report:Report; job:null|{id:string;status:string;rowVersion:number;title:string;description:string;requirements:string;benefits:string|null;location:string|null;company:string;ownerId:string;ownerStatus:string;ownerProtected:boolean}; history:{id:string;action:string;reason:string;createdAt:string;actor:string}[] };
const states:Record<string,string> = {open:'Chờ xử lý',reviewed:'Đang xem xét',resolved:'Đã xử lý',dismissed:'Đã bỏ qua',published:'Đang đăng',closed:'Đã đóng',archived:'Đã ẩn'};
const actions:Record<string,string> = {review:'Đang xem xét',resolved:'Đã xử lý',dismissed:'Bỏ qua báo cáo',hide:'Gỡ tin',warn:'Gỡ và cảnh cáo',suspend:'Tạm khóa người đăng'};
const date = (s:string) => new Date(s).toLocaleString('vi-VN');

export function ReportModeration() {
  const [status,setStatus]=useState('open'); const [type,setType]=useState('job'); const [page,setPage]=useState(1);
  const [data,setData]=useState<ReportPage>(); const [selected,setSelected]=useState<string>(); const [detail,setDetail]=useState<Detail>();
  const [revision,setRevision]=useState(0); const [loading,setLoading]=useState(false); const [detailLoading,setDetailLoading]=useState(false);
  const [busy,setBusy]=useState(false); const [error,setError]=useState(''); const [detailError,setDetailError]=useState(''); const [message,setMessage]=useState('');
  const [reason,setReason]=useState(''); const [confirmAction,setConfirmAction]=useState('');
  useEffect(()=>{
    const controller=new AbortController(); setLoading(true);setError('');setData(undefined);
    void api<ReportPage>(`/api/admin/reports/inbox?status=${status}&targetType=${type}&page=${page}`,{signal:controller.signal}).then(result=>{
      if(controller.signal.aborted)return;
      const last=Math.max(1,Math.ceil(result.total/result.pageSize)); if(page>last){setPage(last);return;} setData(result);
    }).catch(e=>{if(!controller.signal.aborted)setError((e as Error).message)}).finally(()=>{if(!controller.signal.aborted)setLoading(false)});
    return()=>controller.abort();
  },[status,type,page,revision]);
  useEffect(()=>{
    setDetail(undefined);setDetailError('');setReason('');setConfirmAction('');
    if(!selected)return;
    const controller=new AbortController();setDetailLoading(true);
    void api<Detail>(`/api/admin/reports/${selected}`,{signal:controller.signal}).then(x=>{if(!controller.signal.aborted)setDetail(x)}).catch(e=>{if(!controller.signal.aborted)setDetailError((e as Error).message)}).finally(()=>{if(!controller.signal.aborted)setDetailLoading(false)});
    return()=>controller.abort();
  },[selected,revision]);
  async function act(action:string) {
    if(!detail||busy)return;setBusy(true);setDetailError('');setMessage('');
    try {
      await api(`/api/admin/reports/${detail.report.id}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({action,reason:reason.trim(),rowVersion:detail.report.rowVersion,jobRowVersion:detail.job?.rowVersion})});
      setMessage(`${actions[action]}. Đã lưu quyết định và nhật ký.`);setConfirmAction('');setRevision(n=>n+1);
    } catch(e) {
      setConfirmAction('');setDetailError((e as Error).message);
      if(e instanceof ApiError&&e.status===409)setDetail(undefined);
    } finally {setBusy(false)}
  }
  const pages=Math.max(1,Math.ceil((data?.total??0)/10));
  const changeFilter=()=>{setPage(1);setSelected(undefined);setMessage('')};
  return <section className="report-moderation" aria-label="Xử lý báo cáo nội dung">
    <h2>Báo cáo tin tuyển dụng</h2><p>Xem nội dung và lịch sử trước khi quyết định. Gỡ tin giữ lại hồ sơ đã nộp; cảnh cáo được gửi qua thông báo tuyển dụng.</p>
    <div className="row report-filters"><label>Trạng thái báo cáo<select disabled={busy} value={status} onChange={e=>{setStatus(e.target.value);changeFilter()}}><option value="all">Tất cả trạng thái</option>{['open','reviewed','resolved','dismissed'].map(s=><option key={s} value={s}>{states[s]}</option>)}</select></label>
      <label>Loại nội dung<select disabled={busy} value={type} onChange={e=>{setType(e.target.value);changeFilter()}}><option value="job">Tin tuyển dụng</option><option value="public_resume">CV công khai</option><option value="all">Tất cả loại</option></select></label>
      <button className="secondary" disabled={busy||loading} onClick={()=>setRevision(n=>n+1)}>Tải lại báo cáo</button></div>
    {message&&<p role="status">{message}</p>}{loading&&<p role="status">Đang tải báo cáo…</p>}{error&&<p role="alert">{error} <button className="secondary" onClick={()=>setRevision(n=>n+1)}>Thử lại danh sách</button></p>}
    {data&&<><p>{data.total} báo cáo · {data.total?(page-1)*10+1:0}–{Math.min(page*10,data.total)}</p>{!data.total&&<p>Không có báo cáo khớp bộ lọc.</p>}
      <ul className="admin-list">{data.items.map(r=><li key={r.id}><strong>{r.title??(r.targetType==='job'?'Tin không còn tồn tại':'Báo cáo CV công khai')}</strong><p>{states[r.status]} · {date(r.createdAt)}</p><p className="report-text">{r.reason}</p><button className="secondary" disabled={busy} aria-pressed={selected===r.id} onClick={()=>{setSelected(r.id);setMessage('')}}>Xem chi tiết báo cáo</button></li>)}</ul>
      <div className="row"><button className="secondary" disabled={busy||page===1} onClick={()=>setPage(p=>p-1)}>Trang báo cáo trước</button><span>Trang {page} / {pages}</span><button className="secondary" disabled={busy||page===pages} onClick={()=>setPage(p=>p+1)}>Trang báo cáo sau</button></div></>}
    {selected&&<section className="card report-detail" aria-label="Chi tiết báo cáo"><h2>Chi tiết báo cáo</h2>{detailLoading&&<p role="status">Đang tải chi tiết…</p>}{detailError&&<p role="alert">{detailError} <button className="secondary" disabled={busy} onClick={()=>setRevision(n=>n+1)}>Tải lại chi tiết</button></p>}
      {detail&&<><p><strong>Trạng thái:</strong> {states[detail.report.status]} · {date(detail.report.createdAt)}</p><p className="report-text"><strong>Lý do báo cáo:</strong> {detail.report.reason}</p>
        {detail.job?<><h3>{detail.job.title}</h3><p>{detail.job.company} · {detail.job.location} · {states[detail.job.status]??detail.job.status}</p><p className="hint">Nội dung hiện tại của tin, có thể đã thay đổi kể từ lúc báo cáo.</p>{['published','closed'].includes(detail.job.status)&&<a href={`#job?id=${detail.job.id}`}>Mở trang tin tuyển dụng</a>}
          <details open><summary>Nội dung tin để kiểm duyệt</summary><h4>Mô tả</h4><p className="report-text">{detail.job.description}</p><h4>Yêu cầu</h4><p className="report-text">{detail.job.requirements}</p>{detail.job.benefits&&<><h4>Quyền lợi</h4><p className="report-text">{detail.job.benefits}</p></>}</details></>:<p>{detail.report.targetType==='job'?'Tin không còn tồn tại. Bạn vẫn có thể kết luận hoặc bỏ qua báo cáo.':'Báo cáo CV chỉ hỗ trợ ghi kết luận; quyền admin không mở quyền đọc CV riêng tư.'}</p>}
        <h3>Lịch sử xử lý</h3>{!detail.history.length?<p>Chưa có quyết định xử lý.</p>:<ol>{detail.history.map(h=><li key={h.id}><strong>{actions[h.action.replace('report_','')]??h.action}</strong> · {date(h.createdAt)} · {h.actor}<p className="report-text">{h.reason}</p></li>)}</ol>}
        {['open','reviewed'].includes(detail.report.status)?<ValidatedForm onSubmit={e=>e.preventDefault()}><label>Kết luận / Lý do xử lý<textarea required minLength={3} maxLength={500} disabled={busy} value={reason} onChange={e=>setReason(e.target.value)}/></label><p className="hint">Từ 3–500 ký tự. “Đã xử lý” chỉ ghi kết luận; “Gỡ và cảnh cáo” ẩn tin và thông báo lý do cho người đăng. Tạm khóa áp dụng toàn bộ tài khoản; báo cáo tiếp tục ở trạng thái đang xem xét.</p>
          <div className="row">{['review','resolved','dismissed',...(detail.job?['hide','warn','suspend']:[])].filter(a=>a!=='review'||detail.report.status==='open').map(a=><button type="button" className="secondary" key={a} disabled={busy||reason.trim().length<3||(a==='suspend'&&(detail.job?.ownerProtected||detail.job?.ownerStatus!=='active'))} onClick={()=>['hide','warn','suspend'].includes(a)?setConfirmAction(a):void act(a)}>{actions[a]}</button>)}</div></ValidatedForm>:<p>Báo cáo đã kết thúc; không thể ghi đè quyết định. Lịch sử được giữ để đối chiếu.</p>}
      </>}
    </section>}
    <Dialog open={!!confirmAction} title={`Xác nhận ${actions[confirmAction]??''}`} busy={busy} onClose={()=>setConfirmAction('')}><p>{confirmAction==='suspend'?'Tạm khóa toàn bộ tài khoản người đăng, chấm dứt phiên đăng nhập hiện tại. Có thể mở lại tại tab Tài khoản.':'Tin sẽ bị ẩn khỏi danh sách công khai và không nhận ứng tuyển mới. Hồ sơ đã nộp vẫn được giữ.'}</p>{confirmAction==='warn'&&<p>Người đăng sẽ nhận thông báo cảnh cáo kèm lý do dưới đây.</p>}<p className="report-text">{reason}</p><div className="dialog-actions"><button className="secondary" disabled={busy} onClick={()=>setConfirmAction('')}>Hủy</button><button disabled={busy} onClick={()=>void act(confirmAction)}>{busy?'Đang xử lý…':'Xác nhận xử lý'}</button></div></Dialog>
  </section>;
}
