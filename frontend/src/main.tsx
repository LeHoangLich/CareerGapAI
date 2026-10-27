import React, { lazy, Suspense, useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { api, refreshCsrf } from './api';
import { PublicJobBrowser, RecruiterJobManager } from './jobs';
import { CandidatePublicResumeManager, PublicResumeBrowser } from './publicResumes';
import './style.css';
import { FullCvReview } from './fullCvReview';
import { AdminPanel } from './administration';

const ResumeBuilderPanel = lazy(() => import('./resumeBuilder').then(module => ({ default: module.ResumeBuilderPanel })));

type Session = { authenticated: boolean; userId?: string; email?: string; roles: string[]; deletionPending?: boolean; purgeAfter?: string };
type DeletionState = { status: string; requestedAt: string; purgeAfter: string };
type ResumeVersion = { id: string; fileId: string; versionNo: number; documentHash: string; createdAt: string };
type Resume = { id: string; title: string; rowVersion: number; createdAt: string; updatedAt: string; versions: ResumeVersion[] };
type Job = { schemaVersion?: string; id: string; resumeVersionId?: string; status: string; provider?: string; model?: string; outputJson?: string; errorCode?: string };
type ResumeDataRevision = {
  id: string; resumeVersionId: string; schemaVersion: number; revisionNo: number; parentRevisionId: string | null; origin: string;
  structuredData: { schemaVersion: number; skills: { name: string }[] };
  evidence: { skillName: string; sourceType: string; segmentId: string | null; startOffset: number | null; endOffset: number | null; quote: string | null; statement: string | null }[];
  parserVersion: string; dataHash: string; confirmedAt: string | null;
};
type Profile = { userId: string; rowVersion: number; displayName: string; headline: string | null; summary: string | null; location: string | null; contactEmail: string | null; contactPhone: string | null; desiredRole: string | null; preferredWorkMode: string | null; experienceYears: number | null; details: Record<string, unknown>; sharedFields: string[] };

function AccountAccess({ onAuthenticated }: { onAuthenticated: (next: Session) => void }) {
  type Mode = 'login' | 'register' | 'forgot' | 'reset';
  const query = new URLSearchParams(window.location.search);
  const [mode, setMode] = useState<Mode>(query.has('resetToken') ? 'reset' : 'login');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [mailLinks, setMailLinks] = useState<{ link: string }[]>([]);
  const [localMailbox,setLocalMailbox]=useState(false);
  useEffect(()=>{let disposed=false;void api<{localMailboxAvailable:boolean}>('/api/auth/recovery-options').then(x=>{if(!disposed)setLocalMailbox(x.localMailboxAvailable)}).catch(()=>{});return()=>{disposed=true}},[]);
  async function action(fn: () => Promise<void>) {
    setBusy(true); setError(''); setMessage('');
    try { await fn(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  async function signIn(email: string, password: string) {
    const result = await api<{ deletionPending?: boolean; purgeAfter?: string } | undefined>('/api/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password }) });
    await refreshCsrf();
    if (result?.deletionPending) {
      onAuthenticated({ authenticated: true, roles: [], deletionPending: true, purgeAfter: result.purgeAfter });
      return;
    }
    onAuthenticated(await api<Session>('/api/auth/me'));
  }
  return <section className="card login">
    {mode === 'login' && <><h2>Đăng nhập thử nghiệm</h2><p>Dùng tài khoản của bạn để tiếp tục.</p><form onSubmit={e => { e.preventDefault(); const data = new FormData(e.currentTarget); void action(() => signIn(String(data.get('email')), String(data.get('password')))); }}>
      <label>Email<input name="email" type="email" autoComplete="username" required maxLength={254}/></label>
      <label>Mật khẩu<input name="password" type="password" autoComplete="current-password" required maxLength={256}/></label>
      <button disabled={busy}>{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
    </form><div className="auth-links"><button className="link-button" onClick={() => setMode('register')}>Tạo tài khoản</button><button className="link-button" onClick={() => setMode('forgot')}>Quên mật khẩu?</button></div></>}
    {mode === 'register' && <><h2>Tạo tài khoản</h2><p>Chọn Candidate hoặc Recruiter. Vai trò Admin do người phụ trách cấp riêng.</p><form onSubmit={e => { e.preventDefault(); const data = new FormData(e.currentTarget); const email = String(data.get('email')); const password = String(data.get('password')); void action(async () => {
      await api('/api/auth/register', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password, role: data.get('role'), displayName: data.get('displayName') }) });
      await signIn(email, password);
    }); }}>
      <label>Email<input name="email" type="email" autoComplete="username" required maxLength={254}/></label>
      <label>Mật khẩu<input name="password" type="password" autoComplete="new-password" required minLength={12} maxLength={256}/></label>
      <label>Tôi sử dụng CareerGap với vai trò<select name="role" defaultValue="Candidate"><option value="Candidate">Ứng viên</option><option value="Recruiter">Nhà tuyển dụng</option></select></label>
      <label>Tên hiển thị (ứng viên)<input name="displayName" maxLength={150}/></label>
      <button disabled={busy}>{busy ? 'Đang tạo…' : 'Tạo tài khoản'}</button>
    </form><div className="auth-links"><button className="link-button" onClick={() => setMode('login')}>Quay lại đăng nhập</button></div></>}
    {mode === 'forgot' && <><h2>Khôi phục mật khẩu</h2><p>Nếu tài khoản tồn tại và dịch vụ email sẵn sàng, hệ thống sẽ gửi hướng dẫn. Khi dùng mô phỏng local, hộp thư giả xuất hiện bên dưới.</p><form onSubmit={e => { e.preventDefault(); const email = String(new FormData(e.currentTarget).get('email')); void action(async () => { await api('/api/auth/password/forgot', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email }) }); setMessage('Đã nhận yêu cầu. Nếu tài khoản tồn tại, hướng dẫn sẽ được gửi.'); }); }}>
      <label>Email<input name="email" type="email" autoComplete="email" required maxLength={254}/></label><button disabled={busy}>{busy ? 'Đang gửi…' : 'Gửi hướng dẫn'}</button>
    </form>{localMailbox&&<button className="secondary" disabled={busy} onClick={() => void action(async () => { setMailLinks(await api<{ link: string }[]>('/api/dev/mailbox')); })}>Mở hộp thư giả local</button>}
    {mailLinks.map((mail, i) => <p key={i}><a href={mail.link}>Mở liên kết đặt lại mật khẩu giả</a></p>)}<div className="auth-links"><button className="link-button" onClick={() => setMode('login')}>Quay lại đăng nhập</button></div></>}
    {mode === 'reset' && <><h2>Đặt mật khẩu mới</h2><p>Liên kết chỉ dùng một lần. Nếu đã hết hạn, hãy yêu cầu hướng dẫn mới.</p><form onSubmit={e => { e.preventDefault(); const password = String(new FormData(e.currentTarget).get('password')); void action(async () => {
      await api('/api/auth/password/reset', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: query.get('resetEmail'), token: query.get('resetToken'), newPassword: password }) });
      window.history.replaceState({}, '', window.location.pathname); setMessage('Mật khẩu đã được đặt lại. Hãy đăng nhập.'); setMode('login');
    }); }}>
      <label>Mật khẩu mới<input name="password" type="password" autoComplete="new-password" required minLength={12} maxLength={256}/></label><button disabled={busy}>{busy ? 'Đang lưu…' : 'Đặt lại mật khẩu'}</button>
    </form><div className="auth-links"><button className="link-button" onClick={() => setMode('login')}>Quay lại đăng nhập</button></div></>}
    {error && <p role="alert" className="error">{error}</p>}{message && <p role="status">{message}</p>}
  </section>;
}

function CandidateProfilePanel() {
  const [profile, setProfile] = useState<Profile>();
  const [skillsText, setSkillsText] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  useEffect(() => {
    let cancelled = false;
    api<Profile>('/api/profile/me').then(value => {
      if (cancelled) return;
      setProfile(value); setSkillsText(Array.isArray(value.details.skills) ? value.details.skills.join(', ') : '');
    }).catch(e => { if (!cancelled) setError((e as Error).message); });
    return () => { cancelled = true; };
  }, []);
  if (!profile) return <section className="card"><h2>Hồ sơ ứng viên</h2><p role="status">Đang tải hồ sơ…</p>{error && <p role="alert" className="error">{error}</p>}</section>;
  const toggle = (key: string, checked: boolean) => setProfile(current => current ? { ...current, sharedFields: checked ? [...new Set([...current.sharedFields, key])] : current.sharedFields.filter(x => x !== key) } : current);
  const field = (key: keyof Profile, label: string, type = 'text') => <label>{label}<input type={type} value={String(profile[key] ?? '')} onChange={e => { const value = e.currentTarget.value || null; setProfile(current => current ? { ...current, [key]: value } : current); }}/></label>;
  return <section className="card" id="profile"><h2>Hồ sơ và quyền chia sẻ</h2><p><a href={`/?profile=${profile.userId}#shared-profile`}>Liên kết hồ sơ dành cho nhà tuyển dụng</a> chỉ hiển thị các trường bạn cho phép bên dưới.</p>
    <form onSubmit={e => { e.preventDefault(); void (async () => { setBusy(true); setError(''); setMessage(''); try {
      const saved = await api<Profile>('/api/profile/me', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({
        rowVersion: profile.rowVersion, displayName: profile.displayName, headline: profile.headline, summary: profile.summary,
        location: profile.location, contactEmail: profile.contactEmail, contactPhone: profile.contactPhone, desiredRole: profile.desiredRole,
        preferredWorkMode: profile.preferredWorkMode, experienceYears: profile.experienceYears === null ? null : Number(profile.experienceYears),
        details: { skills: skillsText.split(',').map(x => x.trim()).filter(Boolean) }, sharedFields: profile.sharedFields
      }) }); setProfile(saved); setMessage('Hồ sơ đã được lưu.');
    } catch (err) { setError((err as Error).message); } finally { setBusy(false); } })(); }}>
      <div className="profile-fields">{field('displayName','Tên hiển thị')}{field('headline','Tiêu đề nghề nghiệp')}{field('location','Địa điểm')}{field('desiredRole','Vị trí mong muốn')}{field('contactEmail','Email liên hệ','email')}{field('contactPhone','Số điện thoại')}{field('experienceYears','Số năm kinh nghiệm','number')}</div>
      <label>Tóm tắt<textarea value={profile.summary ?? ''} maxLength={4000} onChange={e => setProfile({ ...profile, summary: e.target.value || null })}/></label>
      <label>Kỹ năng, ngăn cách bằng dấu phẩy<input value={skillsText} onChange={e => setSkillsText(e.target.value)}/></label>
      <fieldset><legend>Trường nhà tuyển dụng được xem</legend>{[['display_name','Tên hiển thị'],['headline','Tiêu đề'],['summary','Tóm tắt'],['location','Địa điểm'],['desired_role','Vị trí mong muốn'],['contact_email','Email liên hệ'],['contact_phone','Điện thoại'],['experience_years','Số năm kinh nghiệm'],['skills','Kỹ năng']].map(([key,label]) => <label className="check-option" key={key}><input type="checkbox" checked={profile.sharedFields.includes(key)} onChange={e => toggle(key,e.target.checked)}/>{label}</label>)}</fieldset>
      <button disabled={busy}>{busy ? 'Đang lưu…' : 'Lưu hồ sơ'}</button>
    </form>{error && <p role="alert" className="error">{error}</p>}{message && <p role="status">{message}</p>}
  </section>;
}

function RecruiterProfileLookup() {
  const [candidateId,setCandidateId]=useState(new URLSearchParams(location.search).get('profile')??''); const [items,setItems]=useState<{candidateId:string;displayName:string}[]>([]); const [profile,setProfile]=useState<Record<string,unknown>>(); const [error,setError]=useState('');const [busy,setBusy]=useState(false);
  const labels:Record<string,string>={displayName:'Tên hiển thị',headline:'Tiêu đề nghề nghiệp',summary:'Giới thiệu',location:'Địa điểm',contactEmail:'Email liên hệ',contactPhone:'Điện thoại',desiredRole:'Vị trí mong muốn',preferredWorkMode:'Hình thức làm việc',experienceYears:'Số năm kinh nghiệm',skills:'Kỹ năng',experience:'Kinh nghiệm',education:'Học vấn',projects:'Dự án',certifications:'Chứng chỉ'};
  useEffect(()=>{let disposed=false;void api<{candidateId:string;displayName:string}[]>('/api/public/resumes?page=1').then(value=>{if(!disposed)setItems(value)}).catch(e=>{if(!disposed)setError((e as Error).message)});return()=>{disposed=true}},[]);
  const details=profile?.details&&typeof profile.details==='object'?Object.entries(profile.details):[];
  function text(value:unknown,depth=0):string {if(value==null)return '';if(typeof value==='string'||typeof value==='number')return String(value);if(depth>3)return '';if(Array.isArray(value))return value.slice(0,20).map(x=>text(x,depth+1)).filter(Boolean).join(' · ');if(typeof value==='object')return Object.values(value).slice(0,20).map(x=>text(x,depth+1)).filter(Boolean).join(' · ');return '';}
  return <section className="card" id="shared-profile"><h2>Hồ sơ được chia sẻ</h2><p>Chọn ứng viên từ kho CV hoặc mở liên kết hồ sơ ứng viên gửi. Chỉ hiển thị các trường họ cho phép.</p><form onSubmit={e=>{e.preventDefault();setBusy(true);setProfile(undefined);setError('');void api<Record<string,unknown>>(`/api/profiles/${candidateId}`).then(setProfile).catch(err=>setError((err as Error).message)).finally(()=>setBusy(false));}}><label>Ứng viên<select value={candidateId} onChange={e=>{setCandidateId(e.target.value);setProfile(undefined)}} required><option value="">Chọn ứng viên</option>{candidateId&&!items.some(x=>x.candidateId===candidateId)&&<option value={candidateId}>Ứng viên từ liên kết</option>}{items.map(x=><option key={x.candidateId} value={x.candidateId}>{x.displayName}</option>)}</select></label><button disabled={busy||!candidateId}>{busy?'Đang tải…':'Xem hồ sơ'}</button></form>{error&&<p role="alert" className="error">{error}</p>}{profile&&<dl>{[...Object.entries(profile).filter(([key,value])=>key!=='details'&&value!==null),...details].map(([key,value])=><React.Fragment key={key}><dt>{labels[key]??'Nội dung chia sẻ'}</dt><dd>{text(value)||'Chưa có nội dung'}</dd></React.Fragment>)}</dl>}</section>;
}

function PasswordChangePanel() {
  const [error,setError]=useState('');const [message,setMessage]=useState('');const [busy,setBusy]=useState(false);
  return <section className="card"><h2>Đổi mật khẩu</h2><form onSubmit={e=>{e.preventDefault();const form=e.currentTarget;const data=new FormData(form);void(async()=>{setBusy(true);setError('');setMessage('');try{await api('/api/auth/password/change',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({currentPassword:data.get('currentPassword'),newPassword:data.get('newPassword')})});form.reset();setMessage('Đã đổi mật khẩu.');}catch(err){setError((err as Error).message)}finally{setBusy(false)}})()}}><label>Mật khẩu hiện tại<input type="password" name="currentPassword" required autoComplete="current-password"/></label><label>Mật khẩu mới<input type="password" name="newPassword" required minLength={12} autoComplete="new-password"/></label><button disabled={busy}>{busy?'Đang lưu…':'Đổi mật khẩu'}</button></form>{error&&<p role="alert" className="error">{error}</p>}{message&&<p role="status">{message}</p>}</section>;
}

function AccountDeletionPanel({ onPending }: { onPending: (purgeAfter: string) => void }) {
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  return <section className="card"><h2>Yêu cầu xóa tài khoản</h2>
    <p>Quyền truy cập của tài khoản sẽ bị thu hồi ngay. Bạn có 7 ngày để hủy sau khi xác nhận lại mật khẩu. Hết thời hạn, hệ thống sẽ xóa hoặc ẩn danh dữ liệu chính và dọn file; bản sao lưu chứa dữ liệu đó hết hạn trong tối đa 14 ngày tiếp theo.</p>
    <form onSubmit={e => { e.preventDefault(); const password = String(new FormData(e.currentTarget).get('password')); void (async () => {
      setBusy(true); setError('');
      try {
        const result = await api<DeletionState>('/api/account/deletion-request', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ password }) });
        onPending(result.purgeAfter);
      } catch (err) { setError((err as Error).message); } finally { setBusy(false); }
    })(); }}>
      <label>Nhập mật khẩu để xác nhận<input name="password" type="password" autoComplete="current-password" required maxLength={256}/></label>
      <button className="danger" disabled={busy}>{busy ? 'Đang gửi yêu cầu…' : 'Yêu cầu xóa tài khoản'}</button>
    </form>{error && <p role="alert" className="error">{error}</p>}
  </section>;
}

function AccountDeletionPendingPanel({ session, onAuthenticated }: { session: Session; onAuthenticated: (next: Session) => void }) {
  const [state, setState] = useState<DeletionState>(); const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  useEffect(() => {
    if (session.purgeAfter) return;
    api<DeletionState>('/api/account/deletion-request').then(setState).catch(err => setError((err as Error).message));
  }, [session.purgeAfter]);
  const purgeAfter = session.purgeAfter ?? state?.purgeAfter;
  return <section className="card deletion-pending"><h2>Yêu cầu xóa đang chờ</h2>
    <p>Quyền truy cập tài khoản đã bị thu hồi. Bạn có thể hủy yêu cầu trước mốc dưới đây bằng cách nhập lại mật khẩu. CV đã công khai sẽ vẫn ở trạng thái ẩn sau khi hủy.</p>
    {purgeAfter && <p>Thời điểm dự kiến bắt đầu purge: <strong>{new Date(purgeAfter).toLocaleString('vi-VN')}</strong></p>}
    <form onSubmit={e => { e.preventDefault(); void (async () => {
      setBusy(true); setError('');
      try {
        await api('/api/account/deletion-request/cancel', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ password }) });
        await refreshCsrf();
        onAuthenticated(await api<Session>('/api/auth/me'));
      } catch (err) { setError((err as Error).message); } finally { setBusy(false); }
    })(); }}>
      <label>Mật khẩu hiện tại<input type="password" autoComplete="current-password" required maxLength={256} value={password} onChange={e => setPassword(e.target.value)}/></label>
      <button disabled={busy}>{busy ? 'Đang hủy…' : 'Hủy yêu cầu xóa'}</button>
    </form>
    <button className="secondary" disabled={busy} onClick={() => void (async () => { try { sessionStorage.removeItem(`careergap:builder-recovery:${session.userId}`); await api('/api/auth/logout', { method: 'POST' }); await refreshCsrf(); onAuthenticated({ authenticated: false, roles: [] }); } catch (err) { setError((err as Error).message); } })()}>Đăng xuất</button>
    {error && <p role="alert" className="error">{error}</p>}
  </section>;
}

function App() {
  const [session, setSession] = useState<Session>();
  const [resumes, setResumes] = useState<Resume[]>([]);
  const [selectedResumeId, setSelectedResumeId] = useState('');
  const [selectedResumeVersionId, setSelectedResumeVersionId] = useState('');
  const [editingResumeId, setEditingResumeId] = useState<string>();
  const [resumeTitleDraft, setResumeTitleDraft] = useState('');
  const [page, setPage] = useState(1);
  const [job, setJob] = useState<Job>();
  const [revisions, setRevisions] = useState<ResumeDataRevision[]>([]);
  const [acceptedSkills, setAcceptedSkills] = useState<string[]>([]);
  const [userStatementSkills, setUserStatementSkills] = useState('');
  const [revisionMessage, setRevisionMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const poll = useRef(0);
  const pending = useRef<{ versionId: string; key: string; contract?: string } | undefined>(undefined);
  useEffect(() => { api<Session>('/api/auth/me').then(setSession).catch(e => setError(e.message)).finally(() => setLoading(false)); }, []);
  useEffect(() => {
    if (session?.authenticated && session.roles.includes('Candidate'))
      api<Resume[]>(`/api/resumes?page=${page}`).then(value => {
        setResumes(value);
        setSelectedResumeId(current => value.some(resume => resume.id === current) ? current : '');
        setSelectedResumeVersionId(current => value.some(resume => resume.versions.some(version => version.id === current))
          ? current : (value[0]?.versions[0]?.id ?? ''));
      }).catch(e => setError(e.message));
    else setResumes([]);
  }, [session, page]);
  useEffect(() => {
    if (!job || job.status !== 'succeeded' || !job.resumeVersionId) { setRevisions([]); return; }
    let cancelled = false;
    api<ResumeDataRevision[]>(`/api/resume-versions/${job.resumeVersionId}/revisions`).then(value => {
      if (cancelled) return;
      setRevisions(value);
      const latest = value[0];
      if (latest?.origin === 'ai_extracted' && latest.schemaVersion === 1) setAcceptedSkills(latest.structuredData.skills.map(skill => skill.name));
      else setAcceptedSkills([]);
    }).catch(e => { if (!cancelled) setError(e.message); });
    return () => { cancelled = true; };
  }, [job?.id, job?.status, job?.resumeVersionId]);
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
        else if (pending.current?.versionId === next.resumeVersionId) pending.current = undefined;
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
  const latestRevision = revisions[0];
  const setAuthenticated = (next: Session) => { setSession(next); setError(''); };
  return <main>
    <header><span className="brand">CareerGap <strong>AI</strong></span><span className="badge">Thử nghiệm localhost</span></header>
    {!session?.deletionPending && <nav className="workspace-nav" aria-label="Điều hướng chính"><a href="#jobs">Tìm việc</a>{session?.roles.includes('Candidate')&&<><a href="#cv-builder">Soạn và cải thiện CV</a><a href="#private-resumes">CV riêng</a><a href="#profile">Hồ sơ và chia sẻ</a></>}{session?.roles.includes('Recruiter')&&<><a href="#recruiter-jobs">Tin và hồ sơ ứng tuyển</a><a href="#shared-profile">Hồ sơ chia sẻ</a></>}{session?.roles.some(r=>r==='Candidate'||r==='Recruiter')&&<a href="#public-resumes">Kho CV công khai</a>}{session?.roles.includes('Admin')&&<a href="#administration">Quản trị</a>}{session?.authenticated&&<a href="#account">Tài khoản</a>}</nav>}
    <section className="intro"><p className="eyebrow">NỀN TẢNG HỒ SƠ NGHỀ NGHIỆP</p><h1>Khởi đầu từ CV của bạn.</h1><p>Tìm tin tuyển dụng công khai, chọn một phiên bản CV riêng tư để ứng tuyển và theo dõi hồ sơ đã gửi. Mỗi đơn lưu đúng phiên bản CV và JD tại thời điểm nộp.</p></section>
    {error && <p role="alert" className="error">{error}</p>}
    {!loading && !session?.deletionPending && <PublicJobBrowser session={session}/>}
    {!loading && !session?.deletionPending && <PublicResumeBrowser session={session}/>}
    {loading ? <p role="status">Đang kết nối…</p> : !session?.authenticated ? <AccountAccess onAuthenticated={setAuthenticated}/> : session.deletionPending ? <AccountDeletionPendingPanel session={session} onAuthenticated={setAuthenticated}/> : <>
      <div className="session"><span>{session.email}</span><button className="secondary" disabled={busy} onClick={() => void action(async () => { sessionStorage.removeItem(`careergap:builder-recovery:${session.userId}`); await api('/api/auth/logout', { method: 'POST' }); await refreshCsrf(); setSession({ authenticated: false, roles: [] }); setResumes([]); setSelectedResumeId(''); setSelectedResumeVersionId(''); setJob(undefined); setRevisions([]); pending.current = undefined; })}>Đăng xuất</button></div>
      {session.roles.includes('Candidate') ? <><CandidateProfilePanel/><Suspense fallback={<section className="card"><p role="status">Đang mở trình tạo CV…</p></section>}><ResumeBuilderPanel ownerId={session.userId ?? ''} resumes={resumes} onResumesUpdated={setResumes}/></Suspense><div className="grid"><section className="card"><h2 id="private-resumes">CV riêng của bạn</h2><p>PDF có văn bản hoặc DOCX, tối đa 4 MB. Mỗi CV có thể giữ nhiều phiên bản; file cũ không bị ghi đè và chỉ chính bạn được tải.</p>
        <form onSubmit={e => { e.preventDefault(); const form = e.currentTarget; const data = new FormData(form); void action(async () => {
          const path = selectedResumeId ? `/api/resumes/${selectedResumeId}/versions` : '/api/resumes';
          const created = await api<{ id: string; versionId: string }>(path, { method: 'POST', body: data });
          setPage(1); setSelectedResumeId(created.id); setSelectedResumeVersionId(created.versionId); setResumes(await api<Resume[]>('/api/resumes?page=1')); window.dispatchEvent(new Event('resume-list-updated')); form.reset();
        }); }}>
          <label>Thêm vào CV<input name="file" type="file" accept=".pdf,.docx" required/></label>
          <label>CV đích<select name="targetResumeId" value={selectedResumeId} onChange={e => setSelectedResumeId(e.target.value)}><option value="">Tạo CV mới</option>{resumes.map(r => <option key={r.id} value={r.id}>{r.title}</option>)}</select></label>
          <button disabled={busy}>{busy ? 'Đang xử lý…' : selectedResumeId ? 'Thêm phiên bản' : 'Tạo CV riêng tư'}</button>
        </form>
        <CandidatePublicResumeManager resumes={resumes} selectedVersionId={selectedResumeVersionId}/>
        <ul className="resumes">{resumes.map(r => <li key={r.id}>
          <div className="row resume-heading"><div><strong>{r.title}</strong><small>{r.versions.length} phiên bản</small></div><button className="link-button" disabled={busy} onClick={() => { setEditingResumeId(r.id); setResumeTitleDraft(r.title); }}>Đổi tên</button></div>
          {editingResumeId === r.id && <form className="resume-rename" onSubmit={e => { e.preventDefault(); void action(async () => {
            const saved = await api<{ id: string; title: string; rowVersion: number }> (`/api/resumes/${r.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion: r.rowVersion, title: resumeTitleDraft }) });
            setResumes(current => current.map(item => item.id === saved.id ? { ...item, title: saved.title, rowVersion: saved.rowVersion } : item)); window.dispatchEvent(new Event('resume-list-updated')); setEditingResumeId(undefined);
          }); }}><label>Tên CV<input value={resumeTitleDraft} maxLength={200} required onChange={e => setResumeTitleDraft(e.target.value)}/></label><button disabled={busy}>Lưu tên</button><button type="button" className="secondary" onClick={() => setEditingResumeId(undefined)}>Hủy</button></form>}
          <div className="resume-version-list">{r.versions.map(version => <div className="resume-version row" key={version.id}>
            <label className="resume-version-choice"><input type="radio" name="resume-version" checked={selectedResumeVersionId === version.id} onChange={() => setSelectedResumeVersionId(version.id)}/><span><strong>Phiên bản {version.versionNo}</strong><small>{new Date(version.createdAt).toLocaleString('vi-VN')}</small></span></label>
            <div className="row"><a href={`/api/files/${version.fileId}`}>Tải file</a><button className="secondary" disabled={busy} onClick={() => void action(async () => {
              const keepKey = pending.current?.contract !== 'cv-fields-v3' && pending.current?.versionId === version.id && job?.resumeVersionId === version.id && ['queued', 'running'].includes(job.status);
              const key = keepKey ? pending.current!.key : crypto.randomUUID();
              pending.current = { versionId: version.id, key };
              setRevisions([]); setRevisionMessage(''); setUserStatementSkills('');
              const next = await api<Job>('/api/ai/jobs', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resumeVersionId: version.id, idempotencyKey: key }) });
              if (!['queued', 'running'].includes(next.status)) pending.current = undefined;
              poll.current++; setJob(next);
            })}>Thử trích xuất v{version.versionNo}</button><button className="secondary" disabled={busy} onClick={() => void action(async () => {
              setSelectedResumeVersionId(version.id);
              const keep = pending.current?.contract === 'cv-fields-v3' && pending.current.versionId === version.id && job && ['queued', 'running'].includes(job.status);
              const fullKey = keep ? pending.current!.key : crypto.randomUUID();
              pending.current = { versionId: version.id, key: fullKey, contract: 'cv-fields-v3' };
              const next = await api<Job>('/api/ai/jobs', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resumeVersionId: version.id, idempotencyKey: fullKey, contract: 'cv-fields-v3' }) });
              poll.current++; setJob(next);
            })}>Đọc đầy đủ v{version.versionNo}</button></div>
          </div>)}</div>
        </li>)}</ul>
        {!resumes.length && <p>Chưa có CV trên trang này.</p>}
        <div className="row"><button className="secondary" disabled={page === 1 || busy} onClick={() => setPage(p => p - 1)}>Trước</button><span>Trang {page}</span><button className="secondary" disabled={resumes.length < 20 || busy} onClick={() => setPage(p => p + 1)}>Sau</button></div>
      </section><section className="card"><h2>Kết quả thử nghiệm</h2>{!job ? <p>Chọn “Thử trích xuất” để bắt đầu. Dữ liệu gốc được giữ nguyên.</p> : <>
        <p role="status">{({ queued: 'Đang chờ xử lý', running: 'Đang đọc tài liệu', succeeded: 'Đã xử lý', failed: 'Chưa thể phân tích', cancelled: 'Đã hủy' } as Record<string, string>)[job.status]}</p>
        {job.provider && <p className="badge">{job.provider === 'Fake' ? job.schemaVersion === 'cv-fields-v3' ? 'Mô phỏng cục bộ · không gọi AI thật' : 'Mô phỏng bằng từ khóa · không gọi AI thật' : `DeepSeek · ${job.model}`}</p>}
        {job.status === 'failed' && <p>Tác vụ chưa thành công. File CV vẫn có thể tải về; hệ thống không tạo điểm đánh giá thay thế.</p>}
        {output?.skills && <><p>Kỹ năng được nhắc trực tiếp trong tài liệu:</p><ul>{output.skills.map((s, i) => <li key={i}><strong>{s.name}</strong> — “{s.quote}” <small>{s.evidenceRef}</small></li>)}</ul>{!output.skills.length && <p>Chưa tìm thấy kỹ năng trong phạm vi thử nghiệm.</p>}</>}
        {latestRevision?.schemaVersion === 1 && <section className="revision-review" aria-label="Rà soát dữ liệu trích xuất">
          <h3>Rà soát dữ liệu trích xuất</h3>
          <p>Revision {latestRevision.revisionNo} · {latestRevision.origin === 'ai_extracted' ? 'Chờ bạn xác nhận' : 'Bạn đã xác nhận'} · parser {latestRevision.parserVersion}</p>
          {latestRevision.origin === 'ai_extracted' ? <>
            <p>Chọn các kỹ năng có trong CV. Nội dung bạn tự bổ sung được lưu riêng, không được ghi thành trích dẫn trong tài liệu.</p>
            <fieldset className="revision-skill-options"><legend>Kỹ năng được trích xuất</legend>
              {latestRevision.structuredData.skills.map(skill => <label className="check-option" key={skill.name}>
                <input type="checkbox" checked={acceptedSkills.includes(skill.name)} onChange={e => setAcceptedSkills(current => e.target.checked
                  ? [...new Set([...current, skill.name])]
                  : current.filter(name => name !== skill.name))}/>{skill.name}
              </label>)}
              {!latestRevision.structuredData.skills.length && <p>Không có kỹ năng trích xuất để chọn.</p>}
            </fieldset>
            <label>Kỹ năng bạn tự xác nhận (mỗi dòng một kỹ năng)<textarea value={userStatementSkills} maxLength={2200}
              onChange={e => setUserStatementSkills(e.target.value)}/></label>
            <button disabled={busy} onClick={() => void action(async () => {
              const additions = userStatementSkills.split(/\r?\n/).map(name => name.trim()).filter(Boolean);
              const confirmed = await api<ResumeDataRevision>(`/api/resume-versions/${latestRevision.resumeVersionId}/revisions/${latestRevision.id}/confirm`, {
                method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({
                  baseRevisionNo: latestRevision.revisionNo, acceptedSkillNames: acceptedSkills, userStatementSkills: additions
                })
              });
              setRevisions(current => [confirmed, ...current]); setRevisionMessage('Bản dữ liệu đã được lưu thành revision xác nhận.');
            })}>Xác nhận dữ liệu revision {latestRevision.revisionNo}</button>
          </> : <>
            <p>Dữ liệu đã xác nhận chỉ gắn với đúng phiên bản CV này. Kỹ năng bổ sung của bạn không được trình bày như trích dẫn file.</p>
            <ul>{latestRevision.structuredData.skills.map(skill => {
              const evidence = latestRevision.evidence.find(item => item.skillName === skill.name);
              return <li key={skill.name}><strong>{skill.name}</strong> — {evidence?.sourceType === 'user_statement'
                ? <small>Bạn tự xác nhận; không phải trích dẫn từ CV.</small>
                : <small>“{evidence?.quote ?? ''}” · {evidence?.segmentId}</small>}</li>;
            })}</ul>
          </>}
          {revisionMessage && <p role="status">{revisionMessage}</p>}
        </section>}
        <button className="secondary" disabled={busy} onClick={() => void action(async () => { poll.current++; setJob(await api<Job>(`/api/ai/jobs/${job.id}`)); })}>Kiểm tra lại</button>
      </>}</section></div><FullCvReview versionId={selectedResumeVersionId} refreshKey={`${job?.id}-${job?.status}`}/></> : session.roles.includes('Recruiter') ? <section className="card"><h2>Tài khoản nhà tuyển dụng</h2><p>Recruiter được mở file gốc khi ứng viên chủ động công khai hoặc từ snapshot của đơn thuộc tin mình phụ trách.</p></section> : null}
      {session.roles.includes('Recruiter') && <><RecruiterJobManager/><RecruiterProfileLookup/></>}{session.roles.includes('Admin')&&<AdminPanel/>}<div id="account"><PasswordChangePanel/></div>
      {session.roles.some(role => role === 'Candidate' || role === 'Recruiter') && <AccountDeletionPanel onPending={purgeAfter => setAuthenticated({ authenticated: true, roles: [], deletionPending: true, purgeAfter })}/>}
      <section className="card probe"><div><h2>Kiểm tra PDF tiếng Việt</h2><p>Xuất tài liệu giả để kiểm tra dấu, phân trang và khả năng chọn/copy văn bản.</p></div><button disabled={busy} className="secondary" onClick={() => void action(async () => { const { downloadProbe } = await import('./pdfProbe'); await downloadProbe(); })}>Xuất PDF mẫu giả</button></section>
    </>}
    <footer>Đối chiếu CV–JD theo rubric thử nghiệm · Đơn ứng tuyển giữ nguyên hồ sơ lúc nộp · Người dùng quyết định sàng lọc.</footer>
  </main>;
}
createRoot(document.getElementById('root')!).render(<React.StrictMode><App/></React.StrictMode>);
