import { BusinessAdmin } from './businessIdentity';
import { ReportModeration } from './reportModeration';
import { Dialog, ValidatedForm } from './ui';
import { useEffect, useState } from 'react';
import { api, type PagedResponse } from './api';
type Entry = { id: string; email?: string; displayName?: string | null; accountStatus?: string; roles?: string[]; title?: string; company?: string; status?: string; targetType?: string; reason?: string; action?: string; createdAt?: string; lastLoginAt?: string | null; publishedAt?: string | null; archivedByRole?: string | null; applicationCount?: number; rowVersion?: number; actor?: string };
type Usage = { aiBudgets: { service: string; unit: string; limitAmount: number; spentAmount: number; reservedAmount: number }[]; aiErrors: { provider: string; errorCode: string; count: number }[]; search: { provider: string; actualCredits: number; reservedUnknownCredits: number; requests: number }[]; email: { provider: string; status: string; count: number }[] };
type DayCount = { date: string; count: number };
type Dashboard = {
  days: number;
  totals: {
    users: { total: number; active: number; suspended: number; deletionPending: number; deleted: number; candidates: number; recruiters: number; admins: number };
    jobs: { total: number; draft: number; published: number; closed: number; archived: number };
    applications: { total: number; active: number; rejected: number; withdrawn: number };
    communityPosts: { total: number; published: number; pending: number; hidden: number; deleted: number };
    reportsOpen: { recruitment: number; community: number };
  };
  series: { applicationsByDay: DayCount[]; jobsPublishedByDay: DayCount[]; topJobTags: { tag: string; count: number }[] };
};
const actionNames: Record<string,string> = { suspend:'Tạm khóa', activate:'Mở lại tài khoản', archive:'Ẩn tin', restore:'Khôi phục thành tin đã đóng', resolved:'Đã xử lý', dismissed:'Bỏ qua báo cáo', account_suspend:'Tạm khóa tài khoản', account_activate:'Mở lại tài khoản', job_archive:'Ẩn tin tuyển dụng', job_restore:'Khôi phục tin đã đóng', report_resolved:'Xử lý báo cáo', report_dismissed:'Bỏ qua báo cáo' };
const states: Record<string,string> = { active:'Đang hoạt động', suspended:'Tạm khóa', deletion_pending:'Đang chờ xóa', deleted:'Đã xóa', draft:'Bản nháp', published:'Đang đăng', closed:'Đã đóng', archived:'Đã ẩn', open:'Chờ xử lý', resolved:'Đã xử lý', dismissed:'Đã bỏ qua' };

function BarChart({ title, data }: { title: string; data: DayCount[] }) {
  const max = Math.max(1, ...data.map(x => x.count));
  const total = data.reduce((s, x) => s + x.count, 0);
  const w = Math.max(240, data.length * 10);
  return <section className="card"><h4>{title} ({total} trong kỳ)</h4>
    <svg viewBox={`0 0 ${w} 80`} style={{ width: '100%', height: '110px', display: 'block' }} role="img" aria-label={title}>
      {data.map((d, i) => {
        const h = Math.round((d.count / max) * 64);
        const bw = Math.max(4, Math.floor(w / Math.max(1, data.length)) - 2);
        return <rect key={d.date} x={i * (w / data.length) + 1} y={72 - h} width={bw} height={Math.max(2, h)} fill="currentColor" opacity={d.count > 0 ? 0.85 : 0.2}><title>{`${d.date}: ${d.count}`}</title></rect>;
      })}
    </svg>
    <div className="row"><small>{data[0]?.date}</small><small>{data[data.length - 1]?.date}</small></div>
  </section>;
}

export function AdminPanel() {
  const [tab,setTab]=useState('dashboard'); const [days,setDays]=useState(30); const [page,setPage]=useState(1);
  const [query,setQuery]=useState(''); const [appliedQuery,setAppliedQuery]=useState('');
  const [statusFilter,setStatusFilter]=useState('all'); const [roleFilter,setRoleFilter]=useState('all');
  const [entries,setEntries]=useState<Entry[]>([]); const [total,setTotal]=useState(0); const [usage,setUsage]=useState<Usage>(); const [dashboard,setDashboard]=useState<Dashboard>();
  const [reason,setReason]=useState(''); const [error,setError]=useState(''); const [message,setMessage]=useState(''); const [busy,setBusy]=useState(false);

  function endpoint() {
    if (tab === 'dashboard') return `/api/admin/dashboard?days=${days}`;
    if (tab === 'usage') return '/api/admin/usage';
    const params = new URLSearchParams({ page: String(page), query: appliedQuery });
    if (tab === 'accounts') { params.set('status', statusFilter); params.set('role', roleFilter); }
    if (tab === 'jobs') params.set('status', statusFilter);
    return `/api/admin/${tab}?${params}`;
  }

  async function load() {
    setBusy(true); setError('');
    try {
      const value = await api<PagedResponse<Entry> | Usage | Dashboard>(endpoint());
      if (tab === 'dashboard') setDashboard(value as Dashboard);
      else if (tab === 'usage') setUsage(value as Usage);
      else { const paged = value as PagedResponse<Entry>; setEntries(paged.items); setTotal(paged.total); }
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  useEffect(() => {
    if (tab === 'reports' || tab === 'businesses') { setEntries([]); setTotal(0); setUsage(undefined); setDashboard(undefined); setError(''); setBusy(false); return; }
    let disposed = false; setBusy(true); setError(''); setEntries([]); setTotal(0); setUsage(undefined); setDashboard(undefined);
    const url = tab === 'dashboard' ? `/api/admin/dashboard?days=${days}`
      : tab === 'usage' ? '/api/admin/usage'
      : `/api/admin/${tab}?${new URLSearchParams({ page: String(page), query: appliedQuery, ...(tab === 'accounts' ? { status: statusFilter, role: roleFilter } : tab === 'jobs' ? { status: statusFilter } : {}) })}`;
    void api<PagedResponse<Entry> | Usage | Dashboard>(url).then(value => {
      if (!disposed) {
        if (tab === 'dashboard') setDashboard(value as Dashboard);
        else if (tab === 'usage') setUsage(value as Usage);
        else { const paged = value as PagedResponse<Entry>; setEntries(paged.items); setTotal(paged.total); }
      }
    }).catch(e => { if (!disposed) setError((e as Error).message); }).finally(() => { if (!disposed) setBusy(false); });
    return () => { disposed = true; };
  }, [tab, days, page, appliedQuery, statusFilter, roleFilter]);

  async function act(entry:Entry, action:string) { setBusy(true);setError('');setMessage('');try { await api(`/api/admin/${tab}/${entry.id}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({action,reason,rowVersion:entry.rowVersion})});setMessage(`${actionNames[action]}. Đã ghi nhật ký.`);await load();setReason(''); }catch(e){setError((e as Error).message)}finally{setBusy(false)} }
  return <section className="card admin-panel" id="administration"><h1>Quản trị hệ thống</h1><p>Quản lý tài khoản, tin, số liệu nghiệp vụ và báo cáo. Hồ sơ CV riêng vẫn được bảo vệ theo quyền.</p><nav aria-label="Quản trị">{[['dashboard','Tổng quan'],['accounts','Tài khoản'],['jobs','Tin tuyển dụng'],['reports','Báo cáo'],['businesses','Doanh nghiệp'],['audit','Nhật ký'],['usage','Hạn mức và lỗi']].map(([key,name])=><button type="button" key={key} className={tab===key?'':'secondary'} aria-pressed={tab===key} disabled={busy} onClick={()=>{setTab(key);setPage(1);setQuery('');setAppliedQuery('');setStatusFilter('all');setRoleFilter('all');setMessage('')}}>{name}</button>)}</nav>
    {tab==='dashboard'&&<div className="row"><label>Khoảng thời gian biểu đồ<select value={days} disabled={busy} onChange={e=>setDays(Number(e.target.value))}><option value={7}>7 ngày gần nhất</option><option value={14}>14 ngày gần nhất</option><option value={30}>30 ngày gần nhất</option><option value={90}>90 ngày gần nhất</option></select></label><button type="button" className="secondary" disabled={busy} onClick={()=>void load()}>Làm mới số liệu</button></div>}
    {tab==='accounts'&&<ValidatedForm className="row" onSubmit={e=>{e.preventDefault();setPage(1);setAppliedQuery(query)}}><label>Tìm email hoặc tên<input value={query} maxLength={150} onChange={e=>setQuery(e.target.value)}/></label><label>Trạng thái<select value={statusFilter} onChange={e=>{setStatusFilter(e.target.value);setPage(1)}}><option value="all">Tất cả</option><option value="active">Đang hoạt động</option><option value="suspended">Tạm khóa</option><option value="deletion_pending">Đang chờ xóa</option><option value="deleted">Đã xóa</option></select></label><label>Vai trò<select value={roleFilter} onChange={e=>{setRoleFilter(e.target.value);setPage(1)}}><option value="all">Tất cả</option><option value="Candidate">Ứng viên</option><option value="Recruiter">Nhà tuyển dụng</option><option value="Admin">Quản trị viên</option></select></label><button type="submit" disabled={busy}>Tìm tài khoản</button></ValidatedForm>}
    {tab==='jobs'&&<ValidatedForm className="row" onSubmit={e=>{e.preventDefault();setPage(1);setAppliedQuery(query)}}><label>Tìm tiêu đề hoặc công ty<input value={query} maxLength={150} onChange={e=>setQuery(e.target.value)}/></label><label>Trạng thái tin<select value={statusFilter} onChange={e=>{setStatusFilter(e.target.value);setPage(1)}}><option value="all">Tất cả</option><option value="draft">Bản nháp</option><option value="published">Đang đăng</option><option value="closed">Đã đóng</option><option value="archived">Đã ẩn</option></select></label><button type="submit" disabled={busy}>Lọc tin</button></ValidatedForm>}
    {['accounts','jobs'].includes(tab)&&<label>Lý do xử lý<input value={reason} minLength={3} maxLength={500} onChange={e=>setReason(e.target.value)} placeholder="Ghi lý do trước khi thực hiện"/></label>}
    {busy&&<p role="status">Đang tải hoặc lưu…</p>}{error&&<p role="alert">{error}</p>}{message&&<p role="status">{message}</p>}
    {tab==='reports'&&<ReportModeration/>}{tab==='businesses'&&<BusinessAdmin/>}
    {dashboard&&<section aria-label="Tổng quan nghiệp vụ">
      <div className="form-columns">
        <article className="card"><h3>Người dùng ({dashboard.totals.users.total})</h3><p>Hoạt động: {dashboard.totals.users.active} · Khóa: {dashboard.totals.users.suspended} · Chờ xóa: {dashboard.totals.users.deletionPending}</p><small>Ứng viên: {dashboard.totals.users.candidates} · Nhà tuyển dụng: {dashboard.totals.users.recruiters} · Admin: {dashboard.totals.users.admins}</small></article>
        <article className="card"><h3>Tin tuyển dụng ({dashboard.totals.jobs.total})</h3><p>Đang đăng: {dashboard.totals.jobs.published} · Nháp: {dashboard.totals.jobs.draft} · Đã đóng: {dashboard.totals.jobs.closed}</p><small>Đã ẩn: {dashboard.totals.jobs.archived}</small></article>
        <article className="card"><h3>Đơn ứng tuyển ({dashboard.totals.applications.total})</h3><p>Đang xử lý: {dashboard.totals.applications.active} · Từ chối: {dashboard.totals.applications.rejected}</p><small>Đã rút: {dashboard.totals.applications.withdrawn}</small></article>
        <article className="card"><h3>Cộng đồng &amp; Báo cáo</h3><p>Bài đã đăng: {dashboard.totals.communityPosts.published} · Chờ duyệt: {dashboard.totals.communityPosts.pending}</p><small>Báo cáo chờ xử lý: {dashboard.totals.reportsOpen.recruitment} tin/CV · {dashboard.totals.reportsOpen.community} cộng đồng</small></article>
      </div>
      <div className="form-columns">
        <BarChart title="Đơn ứng tuyển theo ngày" data={dashboard.series.applicationsByDay}/>
        <BarChart title="Tin tuyển dụng công khai theo ngày" data={dashboard.series.jobsPublishedByDay}/>
      </div>
      <section className="card"><h4>Top tag việc làm công khai/đã đóng</h4>{dashboard.series.topJobTags.length===0?<p className="hint">Chưa có tin gắn tag.</p>:<ul>{dashboard.series.topJobTags.map(t=><li key={t.tag}><strong>{t.tag}</strong>: {t.count} tin</li>)}</ul>}</section>
    </section>}
    <ul className="admin-list">{entries.map(entry=><li key={entry.id}><strong>{entry.email??entry.title??(entry.targetType==='public_resume'?'Báo cáo CV công khai':entry.targetType==='job'?'Báo cáo tin tuyển dụng':actionNames[entry.action??'']??'Thao tác quản trị')}</strong><p>{entry.displayName&&`${entry.displayName} · `}{entry.company??entry.roles?.join(', ')} · {states[entry.accountStatus??entry.status??'']??''}{entry.archivedByRole?` (${entry.archivedByRole==='recruiter'?'Recruiter ẩn':'Admin ẩn'})`:''}</p>{entry.applicationCount!==undefined&&<small>Số hồ sơ: {entry.applicationCount} · </small>}{entry.actor&&<small>Người thực hiện: {entry.actor}</small>}{entry.reason&&<p>{entry.reason}</p>}{entry.createdAt&&<small>Tạo lúc: {new Date(entry.createdAt).toLocaleString('vi-VN')}</small>}{entry.publishedAt&&<small> · Đăng lúc: {new Date(entry.publishedAt).toLocaleString('vi-VN')}</small>}{entry.lastLoginAt&&<small> · Đăng nhập gần nhất: {new Date(entry.lastLoginAt).toLocaleString('vi-VN')}</small>}
      <div className="row">{(tab==='accounts'?(entry.roles?.includes('Admin')?[]:entry.accountStatus==='active'?['suspend']:entry.accountStatus==='suspended'?['activate']:[]):tab==='jobs'?(entry.status==='archived'?['restore']:['published','closed'].includes(entry.status??'')?['archive']:[]):tab==='reports'&&entry.status==='open'?['resolved','dismissed']:[]).map(action=><button type="button" key={action} className="secondary" disabled={busy||reason.trim().length<3} onClick={()=>void act(entry,action)}>{actionNames[action]}</button>)}</div></li>)}</ul>
    {usage&&<><h3>AI — đơn vị từng sổ</h3><table><thead><tr><th>Dịch vụ / đơn vị</th><th>Trần</th><th>Đã dùng</th><th>Giữ chỗ</th></tr></thead><tbody>{usage.aiBudgets.map((x,i)=><tr key={i}><td>{x.service} / {x.unit}</td><td>{x.limitAmount}</td><td>{x.spentAmount}</td><td>{x.reservedAmount}</td></tr>)}</tbody></table><h3>Search credit</h3><ul>{usage.search.map((x,i)=><li key={i}>{x.provider}: {x.requests} yêu cầu · {x.actualCredits} credit xác định · {x.reservedUnknownCredits} credit chờ đối soát</li>)}</ul><h3>Email</h3><ul>{usage.email.map((x,i)=><li key={i}>{x.provider}: {x.count} · {x.status==='accepted'?'Provider nhận yêu cầu; chưa chứng minh tới hộp thư': 'Chưa rõ kết quả hoặc đang gửi'}</li>)}</ul><h3>Lỗi AI</h3><ul>{usage.aiErrors.map((x,i)=><li key={i}>{x.provider} · {x.count} tác vụ chưa hoàn thành <details><summary>Mã hỗ trợ</summary>{x.errorCode}</details></li>)}</ul><p className="hint">Sổ trong ứng dụng không bao gồm chi phí gọi ngoài ứng dụng; cần đối chiếu provider và ledger phát triển trước probe thật.</p></>}
    {tab!=='dashboard'&&tab!=='usage'&&tab!=='reports'&&tab!=='businesses'&&<div className="row"><button type="button" className="secondary" disabled={busy||page===1} onClick={()=>setPage(p=>p-1)}>Trang trước</button><span>Trang {page} / {Math.max(1,Math.ceil(total/20))}</span><button type="button" className="secondary" disabled={busy||page*20>=total} onClick={()=>setPage(p=>p+1)}>Trang sau</button></div>}
  </section>;
}
export function ReportButton({targetType,targetId}:{targetType:'job'|'public_resume';targetId:string}) {
  const [open,setOpen]=useState(false);const [reason,setReason]=useState('');const [message,setMessage]=useState('');const [error,setError]=useState('');const [busy,setBusy]=useState(false);
  async function send(){setBusy(true);setError('');try{await api('/api/reports',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({targetType,targetId,reason:reason.trim()})});setMessage('Đã gửi báo cáo tới quản trị viên.');setOpen(false);setReason('')}catch(e){setError((e as Error).message)}finally{setBusy(false)}}
  return <div><button type="button" className="link-button" onClick={()=>{setOpen(true);setError('');setMessage('')}}>Báo cáo nội dung</button>
    <Dialog open={open} title="Báo cáo nội dung" busy={busy} onClose={()=>setOpen(false)}>
      <p>Ghi rõ vấn đề với {targetType==='job'?'tin tuyển dụng':'CV công khai'} để quản trị viên xem xét.</p>
      <ValidatedForm onSubmit={e=>{e.preventDefault();if(reason.trim().length>=3)void send()}}><label>Lý do báo cáo<textarea required minLength={3} value={reason} maxLength={1000} onChange={e=>setReason(e.target.value)}/></label>
        <div className="dialog-actions"><button type="button" className="secondary" disabled={busy} onClick={()=>setOpen(false)}>Hủy</button><button type="submit" disabled={busy||reason.trim().length<3}>{busy?'Đang gửi…':'Gửi báo cáo'}</button></div>
      </ValidatedForm>{error&&<p className="error" role="alert">{error}</p>}
    </Dialog>{message&&<p role="status">{message}</p>}</div>;
}
