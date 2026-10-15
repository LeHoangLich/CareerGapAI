import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { api, refreshCsrf } from './api';
import './style.css';

type Session = { authenticated: boolean; email?: string; roles: string[] };
type Resume = { id: string; title: string; versionId: string; fileId: string; versionNo: number };
type Job = { id: string; status: string; provider?: string; model?: string; outputJson?: string; errorCode?: string };
function App() {
  const [session, setSession] = useState<Session>();
  const [resumes, setResumes] = useState<Resume[]>([]);
  const [page, setPage] = useState(1);
  const [job, setJob] = useState<Job>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const poll = useRef(0);
  const pending = useRef<{ versionId: string; key: string } | undefined>(undefined);
  useEffect(() => { api<Session>('/api/auth/me').then(setSession).catch(e => setError(e.message)).finally(() => setLoading(false)); }, []);
  useEffect(() => {
    if (session?.authenticated) api<Resume[]>(`/api/resumes?page=${page}`).then(setResumes).catch(e => setError(e.message));
  }, [session, page]);
  useEffect(() => {
    if (!job || !['queued', 'running'].includes(job.status)) return;
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout>;
    let attempts = 0;
    async function tick() {
      try {
        const next = await api<Job>(`/api/ai/jobs/${job!.id}`);
        if (cancelled) return;
        setJob(next);
        if (['queued', 'running'].includes(next.status) && ++attempts < 60) timer = setTimeout(tick, Math.min(10000, 1500 + attempts * 500));
        else if (attempts >= 60) setError('Tác vụ vẫn đang chờ. Bạn có thể kiểm tra lại sau.');
      } catch (e) { if (!cancelled) setError((e as Error).message); }
    }
    timer = setTimeout(tick, 1500);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [job?.id, poll.current]); // Status updates must not restart the polling budget.
  async function action(fn: () => Promise<void>) {
    setBusy(true); setError('');
    try { await fn(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  const output = job?.outputJson ? JSON.parse(job.outputJson) as { skills: { name: string; quote: string; evidenceRef: string }[] } : undefined;
  return <main>
    <header><span className="brand">CareerGap <strong>AI</strong></span><span className="badge">Thử nghiệm GĐ1</span></header>
    <section className="intro"><p className="eyebrow">NỀN TẢNG HỒ SƠ NGHỀ NGHIỆP</p><h1>Khởi đầu từ CV của bạn.</h1><p>Tải tài liệu riêng tư và thử trích xuất kỹ năng có bằng chứng. Kết quả thử nghiệm chưa phải đánh giá mức phù hợp với công việc.</p></section>
    {error && <p role="alert" className="error">{error}</p>}
    {loading ? <p role="status">Đang kết nối…</p> : !session?.authenticated ? <section className="card login"><h2>Đăng nhập thử nghiệm</h2><p>Dùng tài khoản được người phụ trách tạo cho môi trường này.</p><form onSubmit={e => { e.preventDefault(); const data = new FormData(e.currentTarget); void action(async () => {
      await api('/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: data.get('email'), password: data.get('password') }) });
      await refreshCsrf(); setSession(await api<Session>('/api/auth/me'));
    }); }}>
      <label>Email<input name="email" type="email" autoComplete="username" required maxLength={254}/></label>
      <label>Mật khẩu<input name="password" type="password" autoComplete="current-password" required maxLength={256}/></label>
      <button disabled={busy}>{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
    </form></section> : <>
      <div className="session"><span>{session.email}</span><button className="secondary" disabled={busy} onClick={() => void action(async () => { await api('/api/auth/logout', { method: 'POST' }); await refreshCsrf(); setSession({ authenticated: false, roles: [] }); setResumes([]); setJob(undefined); pending.current = undefined; })}>Đăng xuất</button></div>
      {session.roles.includes('Candidate') ? <div className="grid"><section className="card"><h2>CV riêng của bạn</h2><p>PDF có văn bản hoặc DOCX, tối đa 4 MB. File chỉ được mở bởi chính tài khoản tải lên trong bản thử này.</p>
        <form onSubmit={e => { e.preventDefault(); const form = e.currentTarget; const data = new FormData(form); void action(async () => {
          await api('/api/resumes', { method: 'POST', body: data }); setPage(1); setResumes(await api<Resume[]>('/api/resumes?page=1')); form.reset();
        }); }}><label>Chọn tài liệu<input name="file" type="file" accept=".pdf,.docx" required/></label><button disabled={busy}>{busy ? 'Đang xử lý…' : 'Tải CV riêng tư'}</button></form>
        <ul className="resumes">{resumes.map(r => <li key={r.versionId}><div><strong>{r.title}</strong><small>Phiên bản {r.versionNo}</small></div><div className="row"><a href={`/api/files/${r.fileId}`}>Tải file</a><button className="secondary" disabled={busy} onClick={() => void action(async () => {
          if (pending.current?.versionId !== r.versionId) pending.current = { versionId: r.versionId, key: crypto.randomUUID() };
          const next = await api<Job>('/api/ai/jobs', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resumeVersionId: r.versionId, idempotencyKey: pending.current.key }) });
          poll.current++; setJob(next);
        })}>Thử trích xuất</button></div></li>)}</ul>
        {!resumes.length && <p>Chưa có CV trên trang này.</p>}
        <div className="row"><button className="secondary" disabled={page === 1 || busy} onClick={() => setPage(p => p - 1)}>Trước</button><span>Trang {page}</span><button className="secondary" disabled={resumes.length < 20 || busy} onClick={() => setPage(p => p + 1)}>Sau</button></div>
      </section><section className="card"><h2>Kết quả thử nghiệm</h2>{!job ? <p>Chọn “Thử trích xuất” để bắt đầu. Dữ liệu gốc được giữ nguyên.</p> : <>
        <p role="status">{({ queued: 'Đang chờ xử lý', running: 'Đang đọc tài liệu', succeeded: 'Đã xử lý', failed: 'Chưa thể phân tích', cancelled: 'Đã hủy' } as Record<string, string>)[job.status]}</p>
        {job.provider && <p className="badge">{job.provider === 'Fake' ? 'Mô phỏng bằng từ khóa · không gọi AI thật' : `DeepSeek · ${job.model}`}</p>}
        {job.status === 'failed' && <p>Tác vụ chưa thành công. File CV vẫn có thể tải về; hệ thống không tạo điểm đánh giá thay thế.</p>}
        {output && <><p>Kỹ năng được nhắc trực tiếp trong tài liệu:</p><ul>{output.skills.map((s, i) => <li key={i}><strong>{s.name}</strong> — “{s.quote}” <small>{s.evidenceRef}</small></li>)}</ul>{!output.skills.length && <p>Chưa tìm thấy kỹ năng trong phạm vi thử nghiệm.</p>}</>}
        <button className="secondary" disabled={busy} onClick={() => void action(async () => { poll.current++; setJob(await api<Job>(`/api/ai/jobs/${job.id}`)); })}>Kiểm tra lại</button>
      </>}</section></div> : <section className="card"><h2>Tài khoản đã đăng nhập</h2><p>Luồng CV private trong GĐ1 dành cho chính ứng viên. Vai trò hiện tại không cấp quyền xem CV riêng của người khác.</p></section>}
      <section className="card probe"><div><h2>Kiểm tra PDF tiếng Việt</h2><p>Xuất tài liệu giả để kiểm tra dấu, phân trang và khả năng chọn/copy văn bản.</p></div><button disabled={busy} className="secondary" onClick={() => void action(async () => { const { downloadProbe } = await import('./pdfProbe'); await downloadProbe(); })}>Xuất PDF mẫu giả</button></section>
    </>}
    <footer>GĐ1 · Chưa mở công khai CV hoặc ứng tuyển. Giao diện tiếng Việt phục vụ thử nghiệm.</footer>
  </main>;
}
createRoot(document.getElementById('root')!).render(<React.StrictMode><App/></React.StrictMode>);
