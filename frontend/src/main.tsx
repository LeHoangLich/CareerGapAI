import '@fontsource/geist/400.css';
import '@fontsource/geist/500.css';
import '@fontsource/geist/600.css';
import './style.css';
import './community.css';
import { AnalysisHistory } from './analysisHistory';
import { Community } from './community';
import { Dialog, ValidatedForm } from './ui';
import React, { lazy, Suspense, useEffect, useMemo, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import { api, ApiError, refreshCsrf, type PagedResponse } from './api';
import { PublicJobBrowser, RecruiterJobManager } from './jobs';
import { CandidatePublicResumeManager, PublicResumeBrowser } from './publicResumes';
import { FullCvReview } from './fullCvReview';
import { AdminPanel } from './administration';
import { PublicHome } from './publicHome';
import { AccountSettingsPanel, type AccountSession } from './accountSettings';
import { StaticLoading, Workspace, WorkspaceIntro } from './workspace';
import { recruiterViews, useWorkspaceNavigation } from './navigation';
import { PersistentView } from './persistentView';
import { jobReturnAfterSignIn, safeReturnAfterSignIn } from './jobLinks';
import { useAiJobPolling } from './useAiJobPolling';
import { FileDropzone, MotionRoot, Spinner } from './motion';

const ResumeBuilderPanel = lazy(() => import('./resumeBuilder').then(module => ({ default: module.ResumeBuilderPanel })));

type Session = AccountSession;
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
  const [registerRole, setRegisterRole] = useState<'Candidate' | 'Recruiter'>('Candidate');
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [invalidReset, setInvalidReset] = useState(false);
  const [mailLinks, setMailLinks] = useState<{ link: string }[]>([]);
  const [localMailbox,setLocalMailbox]=useState(false);
  useEffect(()=>{let disposed=false;void api<{localMailboxAvailable:boolean}>('/api/auth/recovery-options').then(x=>{if(!disposed)setLocalMailbox(x.localMailboxAvailable)}).catch(()=>{});return()=>{disposed=true}},[]);
  function switchMode(next: Mode) {
    setMode(next);
    setError('');
    setMessage('');
    setInvalidReset(false);
    setMailLinks([]);
  }
  async function action(fn: () => Promise<void>) {
    setBusy(true); setError(''); setMessage(''); setInvalidReset(false);
    try { await fn(); } catch (e) {
      setError((e as Error).message);
      setInvalidReset(e instanceof ApiError && e.status === 400 && e.code === 'invalid_or_expired_reset');
    } finally { setBusy(false); }
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
  return <section className="card login" id="access">
    <p className="eyebrow">KHÔNG GIAN CỦA BẠN</p>
    {mode === 'login' && <><h1>Đăng nhập</h1><p>Dùng tài khoản của bạn để tiếp tục.</p><ValidatedForm onSubmit={e => { e.preventDefault(); const data = new FormData(e.currentTarget); void action(() => signIn(String(data.get('email')), String(data.get('password')))); }}>
      <label>Email<input name="email" type="email" autoComplete="username" required maxLength={254}/></label>
      <label>Mật khẩu<input name="password" type="password" autoComplete="current-password" required maxLength={256}/></label>
      <button type="submit" disabled={busy}>{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
    </ValidatedForm><div className="auth-links"><button type="button" className="link-button" onClick={() => switchMode('register')}>Tạo tài khoản</button><button type="button" className="link-button" onClick={() => switchMode('forgot')}>Quên mật khẩu?</button></div></>}
    {mode === 'register' && <><h1>Tạo tài khoản</h1><p>Chọn Ứng viên hoặc Nhà tuyển dụng. Vai trò Quản trị viên do người phụ trách cấp riêng. Nhà tuyển dụng cần email tên miền doanh nghiệp; email, mã số thuế và pháp nhân phải được xác minh trước khi đăng tin.</p><ValidatedForm onSubmit={e => { e.preventDefault(); const data = new FormData(e.currentTarget); const email = String(data.get('email')); const password = String(data.get('password')); void action(async () => {
      await api('/api/auth/register', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email, password, role: registerRole, displayName: registerRole === 'Candidate' ? data.get('displayName') : null }) });
      await signIn(email, password);
    }); }}>
      <label>Email<input name="email" type="email" autoComplete="username" required maxLength={254}/></label>
      <label>Mật khẩu<input name="password" type="password" autoComplete="new-password" required minLength={12} maxLength={256}/></label>
      <label>Tôi sử dụng CareerGap với vai trò<select name="role" value={registerRole} onChange={e => setRegisterRole(e.target.value as 'Candidate' | 'Recruiter')}><option value="Candidate">Ứng viên</option><option value="Recruiter">Nhà tuyển dụng</option></select></label>
      {registerRole === 'Candidate' && <label>Tên hiển thị (ứng viên)<input name="displayName" maxLength={150}/></label>}
      <button type="submit" disabled={busy}>{busy ? 'Đang tạo…' : 'Tạo tài khoản'}</button>
    </ValidatedForm><div className="auth-links"><button type="button" className="link-button" onClick={() => switchMode('login')}>Quay lại đăng nhập</button></div></>}
    {mode === 'forgot' && <><h1>Khôi phục mật khẩu</h1><p>Nếu tài khoản tồn tại và dịch vụ email sẵn sàng, hệ thống sẽ gửi hướng dẫn. Khi dùng mô phỏng local, hộp thư giả xuất hiện bên dưới.</p><ValidatedForm onSubmit={e => { e.preventDefault(); const email = String(new FormData(e.currentTarget).get('email')); void action(async () => { await api('/api/auth/password/forgot', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email }) }); setMessage('Đã nhận yêu cầu. Nếu tài khoản tồn tại, hướng dẫn sẽ được gửi.'); }); }}>
      <label>Email<input name="email" type="email" autoComplete="email" required maxLength={254}/></label><button type="submit" disabled={busy}>{busy ? 'Đang gửi…' : 'Gửi hướng dẫn'}</button>
    </ValidatedForm>{localMailbox&&<button type="button" className="secondary" disabled={busy} onClick={() => void action(async () => { setMailLinks(await api<{ link: string }[]>('/api/dev/mailbox')); })}>Mở hộp thư giả local</button>}
    {mailLinks.map((mail, i) => <p key={i}><a href={mail.link}>Mở liên kết đặt lại mật khẩu giả</a></p>)}<div className="auth-links"><button type="button" className="link-button" onClick={() => switchMode('login')}>Quay lại đăng nhập</button></div></>}
    {mode === 'reset' && <><h1>Đặt mật khẩu mới</h1><p>Liên kết chỉ dùng một lần. Nếu đã hết hạn, hãy yêu cầu hướng dẫn mới.</p><ValidatedForm onSubmit={e => { e.preventDefault(); const password = String(new FormData(e.currentTarget).get('password')); void action(async () => {
      await api('/api/auth/password/reset', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ email: query.get('resetEmail'), token: query.get('resetToken'), newPassword: password }) });
      window.history.replaceState({}, '', window.location.pathname); setMessage('Mật khẩu đã được đặt lại. Hãy đăng nhập.'); setMode('login');
    }); }}>
      <label>Mật khẩu mới<input name="password" type="password" autoComplete="new-password" required minLength={12} maxLength={256}/></label><button type="submit" disabled={busy}>{busy ? 'Đang lưu…' : 'Đặt lại mật khẩu'}</button>
    </ValidatedForm><div className="auth-links"><button type="button" className="link-button" onClick={() => switchMode('login')}>Quay lại đăng nhập</button>
      {invalidReset && <button type="button" className="link-button" disabled={busy} onClick={() => {
        window.history.replaceState({}, '', window.location.pathname + window.location.hash);
        switchMode('forgot');
      }}>Yêu cầu gửi lại liên kết mới</button>}
    </div></>}
    {error && <p role="alert" className="error">{error}</p>}
{message && <p role="status">{message}</p>}
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
  const field = (key: keyof Profile, label: string, type = 'text') => <label>{label}<input type={type} required={key === 'displayName'} min={type === 'number' ? 0 : undefined} max={type === 'number' ? 99.99 : undefined} step={type === 'number' ? 0.01 : undefined} value={String(profile[key] ?? '')} onChange={e => { const value = e.currentTarget.value || null; setProfile(current => current ? { ...current, [key]: value } : current); }}/></label>;
  return <section className="card" id="profile"><h2>Hồ sơ và quyền chia sẻ</h2><p><a href={`/?profile=${profile.userId}#shared-profile`}>Liên kết hồ sơ dành cho nhà tuyển dụng</a> chỉ hiển thị các trường bạn cho phép bên dưới.</p>
    <ValidatedForm onSubmit={e => { e.preventDefault(); void (async () => { setBusy(true); setError(''); setMessage(''); try {
      const saved = await api<Profile>('/api/profile/me', { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({
        rowVersion: profile.rowVersion, displayName: profile.displayName, headline: profile.headline, summary: profile.summary,
        location: profile.location, contactEmail: profile.contactEmail, contactPhone: profile.contactPhone, desiredRole: profile.desiredRole,
        preferredWorkMode: profile.preferredWorkMode, experienceYears: profile.experienceYears === null ? null : Number(profile.experienceYears),
        details: { skills: skillsText.split(',').map(x => x.trim()).filter(Boolean) }, sharedFields: profile.sharedFields
      }) }); setProfile(saved); setMessage('Hồ sơ đã được lưu.');
    } catch (err) { setError((err as Error).message); } finally { setBusy(false); } })(); }}>
      <div className="profile-fields">{field('displayName','Tên hiển thị')}{field('headline','Tiêu đề nghề nghiệp')}{field('location','Địa điểm')}{field('desiredRole','Vị trí mong muốn')}
        <label>Hình thức làm việc mong muốn<select value={profile.preferredWorkMode ?? 'unspecified'} onChange={e => setProfile(current => current ? { ...current, preferredWorkMode: e.target.value } : current)}><option value="unspecified">Chưa xác định</option><option value="onsite">Tại văn phòng</option><option value="hybrid">Linh hoạt</option><option value="remote">Từ xa</option></select></label>
        {field('contactEmail','Email liên hệ','email')}{field('contactPhone','Số điện thoại')}{field('experienceYears','Số năm kinh nghiệm','number')}</div>
      <label>Tóm tắt<textarea value={profile.summary ?? ''} maxLength={4000} onChange={e => setProfile({ ...profile, summary: e.target.value || null })}/></label>
      <label>Kỹ năng, ngăn cách bằng dấu phẩy<input value={skillsText} onChange={e => setSkillsText(e.target.value)}/></label>
      <fieldset><legend>Trường nhà tuyển dụng được xem</legend>{[['display_name','Tên hiển thị'],['headline','Tiêu đề'],['summary','Tóm tắt'],['location','Địa điểm'],['desired_role','Vị trí mong muốn'],['preferred_work_mode','Hình thức làm việc'],['contact_email','Email liên hệ'],['contact_phone','Điện thoại'],['experience_years','Số năm kinh nghiệm'],['skills','Kỹ năng']].map(([key,label]) => <label className="check-option" key={key}><input type="checkbox" checked={profile.sharedFields.includes(key)} onChange={e => toggle(key,e.target.checked)}/>{label}</label>)}</fieldset>
      <button type="submit" disabled={busy}>{busy ? 'Đang lưu…' : 'Lưu hồ sơ'}</button>
    </ValidatedForm>{error && <p role="alert" className="error">{error}</p>}{message && <p role="status">{message}</p>}
  </section>;
}

function readSharedProfileId() {
  return new URLSearchParams(window.location.search).get('profile')
    ?? new URLSearchParams(window.location.hash.split('?')[1] ?? '').get('profile')
    ?? '';
}

function RecruiterProfileLookup({ active }: { active: boolean }) {
  const [candidateId,setCandidateId]=useState(readSharedProfileId); const [items,setItems]=useState<{candidateId:string;displayName:string}[]>([]); const [profile,setProfile]=useState<Record<string,unknown>>(); const [error,setError]=useState('');const [busy,setBusy]=useState(false);const [reloadTick,setReloadTick]=useState(0);
  const labels:Record<string,string>={displayName:'Tên hiển thị',headline:'Tiêu đề nghề nghiệp',summary:'Giới thiệu',location:'Địa điểm',contactEmail:'Email liên hệ',contactPhone:'Điện thoại',desiredRole:'Vị trí mong muốn',preferredWorkMode:'Hình thức làm việc',experienceYears:'Số năm kinh nghiệm',skills:'Kỹ năng',experience:'Kinh nghiệm',education:'Học vấn',projects:'Dự án',certifications:'Chứng chỉ'};
  useEffect(()=>{let disposed=false;void api<PagedResponse<{candidateId:string;displayName:string}>>('/api/public/resumes?page=1').then(value=>{if(!disposed)setItems(value.items)}).catch(e=>{if(!disposed)setError((e as Error).message)});return()=>{disposed=true}},[]);
  useEffect(()=>{const onHash=()=>{const next=readSharedProfileId();if(next){setCandidateId(next);setProfile(undefined);}};window.addEventListener('hashchange',onHash);return()=>window.removeEventListener('hashchange',onHash);},[]);
  useEffect(()=>{
    if(!active||!candidateId)return;
    const controller=new AbortController();
    setBusy(true);setProfile(undefined);setError('');
    void api<Record<string,unknown>>(`/api/profiles/${encodeURIComponent(candidateId)}`,{signal:controller.signal})
      .then(value=>{if(!controller.signal.aborted)setProfile(value);})
      .catch(err=>{if(!controller.signal.aborted)setError((err as Error).message);})
      .finally(()=>{if(!controller.signal.aborted)setBusy(false);});
    return()=>controller.abort();
  },[active,candidateId,reloadTick]);
  const details=profile?.details&&typeof profile.details==='object'?Object.entries(profile.details):[];
  function text(value:unknown,depth=0):string {if(value==null)return '';if(typeof value==='string'||typeof value==='number')return String(value);if(depth>3)return '';if(Array.isArray(value))return value.slice(0,20).map(x=>text(x,depth+1)).filter(Boolean).join(' · ');if(typeof value==='object')return Object.values(value).slice(0,20).map(x=>text(x,depth+1)).filter(Boolean).join(' · ');return '';}
  return <section className="card" id="shared-profile"><h1>Hồ sơ được chia sẻ</h1><p>Chọn ứng viên từ kho CV hoặc mở liên kết hồ sơ ứng viên gửi. Chỉ hiển thị các trường họ cho phép.</p><ValidatedForm onSubmit={e=>{e.preventDefault();if(candidateId)setReloadTick(v=>v+1);}}><label>Ứng viên<select value={candidateId} onChange={e=>{setCandidateId(e.target.value);setProfile(undefined)}} required><option value="">Chọn ứng viên</option>{candidateId&&!items.some(x=>x.candidateId===candidateId)&&<option value={candidateId}>Ứng viên từ liên kết</option>}{items.map(x=><option key={x.candidateId} value={x.candidateId}>{x.displayName}</option>)}</select></label><button type="submit" disabled={busy||!candidateId}>{busy?'Đang tải…':'Xem hồ sơ'}</button></ValidatedForm>{error&&<p role="alert" className="error">{error}</p>}{profile&&<dl>{[...Object.entries(profile).filter(([key,value])=>key!=='details'&&value!==null),...details].map(([key,value])=><React.Fragment key={key}><dt>{labels[key]??'Nội dung chia sẻ'}</dt><dd>{text(value)||'Chưa có nội dung'}</dd></React.Fragment>)}</dl>}</section>;
}

function AccountDeletionPanel({ onPending }: { onPending: (purgeAfter: string) => void }) {
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const [open, setOpen] = useState(false);
  return <section className="card account-deletion"><h2>Yêu cầu xóa tài khoản</h2>
    <p>Quyền truy cập của tài khoản sẽ bị thu hồi ngay. Bạn có 7 ngày để hủy sau khi xác nhận lại mật khẩu. Hết thời hạn, hệ thống sẽ xóa hoặc ẩn danh dữ liệu chính và dọn file; bản sao lưu chứa dữ liệu đó hết hạn trong tối đa 14 ngày tiếp theo.</p>
    <button type="button" className="danger" onClick={() => setOpen(true)}>Yêu cầu xóa tài khoản</button>
    <Dialog open={open} title="Xác nhận yêu cầu xóa tài khoản" busy={busy} onClose={() => { setOpen(false); setError(''); }}>
    <p>Quyền truy cập sẽ bị thu hồi và CV công khai sẽ bị ẩn ngay. Bạn có 7 ngày để hủy yêu cầu trước khi dữ liệu được xóa hoặc ẩn danh.</p>
    <ValidatedForm onSubmit={e => { e.preventDefault(); const password = String(new FormData(e.currentTarget).get('password')); void (async () => {
      setBusy(true); setError('');
      try {
        const result = await api<DeletionState>('/api/account/deletion-request', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ password }) });
        onPending(result.purgeAfter);
      } catch (err) { setError((err as Error).message); } finally { setBusy(false); }
    })(); }}>
      <label>Nhập mật khẩu để xác nhận<input name="password" type="password" autoComplete="current-password" required maxLength={256}/></label>
      <div className="dialog-actions"><button type="button" className="secondary" disabled={busy} onClick={() => setOpen(false)}>Giữ tài khoản</button><button type="submit" className="danger" disabled={busy}>{busy ? 'Đang gửi yêu cầu…' : 'Xác nhận yêu cầu xóa'}</button></div>
    </ValidatedForm>{error && <p role="alert" className="error">{error}</p>}
    </Dialog>
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
    <ValidatedForm onSubmit={e => { e.preventDefault(); void (async () => {
      setBusy(true); setError('');
      try {
        await api('/api/account/deletion-request/cancel', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ password }) });
        await refreshCsrf();
        onAuthenticated(await api<Session>('/api/auth/me'));
      } catch (err) { setError((err as Error).message); } finally { setBusy(false); }
    })(); }}>
      <label>Mật khẩu hiện tại<input type="password" autoComplete="current-password" required maxLength={256} value={password} onChange={e => setPassword(e.target.value)}/></label>
      <button type="submit" disabled={busy}>{busy ? 'Đang hủy…' : 'Hủy yêu cầu xóa'}</button>
    </ValidatedForm>
    <button type="button" className="secondary" disabled={busy} onClick={() => void (async () => { try { sessionStorage.removeItem(`careergap:builder-recovery:${session.userId}`); await api('/api/auth/logout', { method: 'POST' }); await refreshCsrf(); onAuthenticated({ authenticated: false, roles: [] }); } catch (err) { setError((err as Error).message); } })()}>Đăng xuất</button>
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
  const [resumesTotal, setResumesTotal] = useState(0);
  const [job, setJob] = useState<Job>();
  const [revisions, setRevisions] = useState<ResumeDataRevision[]>([]);
  const [acceptedSkills, setAcceptedSkills] = useState<string[]>([]);
  const [userStatementSkills, setUserStatementSkills] = useState('');
  const [revisionMessage, setRevisionMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const activeView = useWorkspaceNavigation(session, loading);
  const [pollCount, setPollCount] = useState(0);
  const pending = useRef<{ versionId: string; key: string; contract?: string } | undefined>(undefined);
  useEffect(() => { api<Session>('/api/auth/me').then(setSession).catch(e => setError(e.message)).finally(() => setLoading(false)); }, []);
  useEffect(() => {
    if (session?.authenticated && session.roles.includes('Candidate'))
      api<PagedResponse<Resume>>(`/api/resumes?page=${page}`).then(value => {
        setResumes(value.items);
        setResumesTotal(value.total);
        setSelectedResumeId(current => value.items.some(resume => resume.id === current) ? current : '');
        setSelectedResumeVersionId(current => value.items.some(resume => resume.versions.some(version => version.id === current))
          ? current : (value.items[0]?.versions[0]?.id ?? ''));
      }).catch(e => setError(e.message));
    else { setResumes([]); setResumesTotal(0); }
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
  const extractPollKey = job && ['queued', 'running'].includes(job.status) ? `${job.id}:${pollCount}` : undefined;
  useAiJobPolling({
    pollKey: extractPollKey,
    active: activeView === 'private-resumes',
    maxAttempts: 60,
    initialDelayMs: 1500,
    intervalMs: attempt => Math.min(10000, 1500 + attempt * 500),
    tick: async signal => {
      const next = await api<Job>(`/api/ai/jobs/${job!.id}`, { signal });
      if (signal.aborted) return false;
      setJob(next);
      if (!['queued', 'running'].includes(next.status)) {
        if (pending.current?.versionId === next.resumeVersionId) pending.current = undefined;
        return true;
      }
      return false;
    },
    onError: e => setError((e as Error).message),
    timeoutMessage: 'Tác vụ vẫn đang chờ. Bạn có thể kiểm tra lại sau.',
  });
  async function action(fn: () => Promise<void>) {
    setBusy(true); setError('');
    try { await fn(); } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  const output = useMemo(() => {
    if (!job?.outputJson) return undefined;
    try {
      return JSON.parse(job.outputJson) as { skills: { name: string; quote: string; evidenceRef: string }[] };
    } catch {
      return undefined;
    }
  }, [job?.outputJson]);
  const latestRevision = revisions[0];
  const setAuthenticated = (next: Session) => {
    setSession(next); setError('');
    const destination = safeReturnAfterSignIn(window.location.hash, next) ?? jobReturnAfterSignIn(window.location.hash);
    if (destination && next.authenticated && !next.deletionPending) window.location.hash = destination;
  };
  return <Workspace key={session?.deletionPending ? 'deletion-pending' : session?.authenticated ? session.userId ?? 'authenticated' : 'guest'} session={session} activeView={activeView} busy={busy} onLogout={() => void action(async () => { sessionStorage.removeItem(`careergap:builder-recovery:${session?.userId}`); await api('/api/auth/logout', { method: 'POST' }); await refreshCsrf(); setSession({ authenticated: false, roles: [] }); setResumes([]); setSelectedResumeId(''); setSelectedResumeVersionId(''); setJob(undefined); setRevisions([]); pending.current = undefined; window.location.hash = 'access'; })}>
    {error && <p role="alert" className="error">{error}</p>}
    {!loading && !session?.deletionPending && <PersistentView active={activeView === 'home'} name="home"><PublicHome active={activeView === 'home'}/></PersistentView>}
    {!loading && !session?.deletionPending && <PersistentView active={activeView === 'community'} name="community"><Community session={session} active={activeView === 'community'}/></PersistentView>}
    {!loading && !session?.deletionPending && <PublicJobBrowser session={session} activeView={activeView} intro={<WorkspaceIntro recruiter={session?.roles.includes('Recruiter')}/>}/>}
    {!loading && !session?.deletionPending && <PersistentView active={activeView === 'public-resumes'} name="public-resumes"><PublicResumeBrowser session={session}/></PersistentView>}
    {loading ? <StaticLoading label="Đang kết nối…"/> : !session?.authenticated ? <PersistentView active={activeView === 'access'} name="access"><AccountAccess onAuthenticated={setAuthenticated}/></PersistentView> : session.deletionPending ? <PersistentView active name="account"><AccountDeletionPendingPanel session={session} onAuthenticated={setAuthenticated}/></PersistentView> : <>
      {session.roles.some(role => role === 'Candidate' || role === 'Recruiter') && <PersistentView active={activeView === 'analysis-history'} name="analysis-history"><AnalysisHistory active={activeView === 'analysis-history'}/></PersistentView>}
      {session.roles.includes('Candidate') ? <><PersistentView active={activeView === 'profile'} name="profile"><CandidateProfilePanel/></PersistentView><PersistentView active={activeView === 'cv-builder'} name="cv-builder"><Suspense fallback={<section className="card"><StaticLoading label="Đang mở trình tạo CV…"/></section>}><ResumeBuilderPanel ownerId={session.userId ?? ''} resumes={resumes} onResumesUpdated={next => { setPage(1); setResumes(next); }} pdfProbe={import.meta.env.DEV ? <section className="card probe"><div><h2>Kiểm tra PDF tiếng Việt</h2><p>Xuất tài liệu mẫu để kiểm tra dấu, phân trang và khả năng chọn/copy văn bản.</p></div><button type="button" disabled={busy} className="secondary" onClick={() => void action(async () => { const { downloadProbe } = await import('./pdfProbe'); await downloadProbe(); })}>Xuất PDF mẫu</button></section> : undefined}/></Suspense></PersistentView><PersistentView active={activeView === 'private-resumes'} name="private-resumes"><div className="resume-workspace-grid"><section className="card" id="private-resumes"><h2>CV riêng của bạn</h2><p>PDF có văn bản hoặc DOCX, tối đa 4 MB. Mỗi CV có thể giữ nhiều phiên bản; file cũ không bị ghi đè và chỉ chính bạn được tải.</p>
        <ValidatedForm onSubmit={e => { e.preventDefault(); const form = e.currentTarget; const data = new FormData(form); void action(async () => {
          const path = selectedResumeId ? `/api/resumes/${selectedResumeId}/versions` : '/api/resumes';
          const created = await api<{ id: string; versionId: string }>(path, { method: 'POST', body: data });
          const nextPage = await api<PagedResponse<Resume>>('/api/resumes?page=1');
          setPage(1); setSelectedResumeId(created.id); setSelectedResumeVersionId(created.versionId); setResumes(nextPage.items); setResumesTotal(nextPage.total); window.dispatchEvent(new Event('resume-list-updated')); form.reset();
        }); }}>
          <FileDropzone
            name="file"
            accept=".pdf,.docx"
            required
            label="Thêm vào CV"
            hint="Kéo thả PDF hoặc DOCX (tối đa 4 MB), hoặc nhấn để chọn file"
            disabled={busy}
          />
          <label>CV đích<select name="targetResumeId" value={selectedResumeId} onChange={e => setSelectedResumeId(e.target.value)}><option value="">Tạo CV mới</option>{resumes.map(r => <option key={r.id} value={r.id}>{r.title}</option>)}</select></label>
          <button type="submit" disabled={busy}>{busy && <Spinner size={15} />}{busy ? 'Đang xử lý…' : selectedResumeId ? 'Thêm phiên bản' : 'Tạo CV riêng tư'}</button>
        </ValidatedForm>
        <CandidatePublicResumeManager resumes={resumes} selectedVersionId={selectedResumeVersionId}/>
        <ul className="resumes">{resumes.map(r => <li key={r.id}>
          <div className="row resume-heading"><div><strong>{r.title}</strong><small>{r.versions.length} phiên bản</small></div><button type="button" className="link-button" disabled={busy} onClick={() => { setEditingResumeId(r.id); setResumeTitleDraft(r.title); }}>Đổi tên</button></div>
          {editingResumeId === r.id && <ValidatedForm className="resume-rename" onSubmit={e => { e.preventDefault(); void action(async () => {
            const saved = await api<{ id: string; title: string; rowVersion: number }> (`/api/resumes/${r.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion: r.rowVersion, title: resumeTitleDraft }) });
            setResumes(current => current.map(item => item.id === saved.id ? { ...item, title: saved.title, rowVersion: saved.rowVersion } : item)); window.dispatchEvent(new Event('resume-list-updated')); setEditingResumeId(undefined);
          }); }}><label>Tên CV<input value={resumeTitleDraft} maxLength={200} required onChange={e => setResumeTitleDraft(e.target.value)}/></label><button type="submit" disabled={busy}>Lưu tên</button><button type="button" className="secondary" onClick={() => setEditingResumeId(undefined)}>Hủy</button></ValidatedForm>}
          <div className="resume-version-list">{r.versions.map(version => <div className="resume-version row" key={version.id}>
            <label className="resume-version-choice"><input type="radio" name="resume-version" checked={selectedResumeVersionId === version.id} onChange={() => setSelectedResumeVersionId(version.id)}/><span><strong>Phiên bản {version.versionNo}</strong><small>{new Date(version.createdAt).toLocaleString('vi-VN')}</small></span></label>
            <div className="row"><a href={`/api/files/${version.fileId}`}>Tải file</a><button type="button" disabled={busy} onClick={() => void action(async () => {
              setSelectedResumeVersionId(version.id);
              const keep = pending.current?.contract === 'cv-fields-v3' && pending.current.versionId === version.id && job && ['queued', 'running'].includes(job.status);
              const fullKey = keep ? pending.current!.key : crypto.randomUUID();
              pending.current = { versionId: version.id, key: fullKey, contract: 'cv-fields-v3' };
              const next = await api<Job>('/api/ai/jobs', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resumeVersionId: version.id, idempotencyKey: fullKey, contract: 'cv-fields-v3' }) });
              setPollCount(c => c + 1); setJob(next);
            })}>Phân tích nội dung CV v{version.versionNo}</button><details><summary>Tùy chọn nâng cao</summary><button type="button" className="secondary" disabled={busy} onClick={() => void action(async () => {
              const keepKey = pending.current?.contract !== 'cv-fields-v3' && pending.current?.versionId === version.id && job?.resumeVersionId === version.id && ['queued', 'running'].includes(job.status);
              const key = keepKey ? pending.current!.key : crypto.randomUUID();
              pending.current = { versionId: version.id, key };
              setRevisions([]); setRevisionMessage(''); setUserStatementSkills('');
              const next = await api<Job>('/api/ai/jobs', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resumeVersionId: version.id, idempotencyKey: key }) });
              if (!['queued', 'running'].includes(next.status)) pending.current = undefined;
              setPollCount(c => c + 1); setJob(next);
            })}>Trích xuất nhanh kỹ năng v{version.versionNo}</button></details></div>
          </div>)}</div>
        </li>)}</ul>
        {!resumes.length && <p>Chưa có CV trên trang này.</p>}
        <div className="row"><button type="button" className="secondary" disabled={page === 1 || busy} onClick={() => setPage(p => p - 1)}>Trang trước</button><span>Trang {page} / {Math.max(1, Math.ceil(resumesTotal / 20))}</span><button type="button" className="secondary" disabled={page * 20 >= resumesTotal || busy} onClick={() => setPage(p => p + 1)}>Trang sau</button></div>
      </section><section className="card"><h2>Kết quả phân tích CV</h2>{!job ? <p>Chọn “Phân tích nội dung CV” để bắt đầu. Dữ liệu gốc được giữ nguyên.</p> : <>
        <p role="status">{({ queued: 'Đang chờ xử lý', running: 'Đang đọc tài liệu', succeeded: 'Đã xử lý', failed: 'Chưa thể phân tích', cancelled: 'Đã hủy' } as Record<string, string>)[job.status]}</p>
        {job.provider && <p className="badge">{job.provider === 'Fake' ? job.schemaVersion === 'cv-fields-v3' ? 'Mô phỏng cục bộ · không gọi AI thật' : 'Mô phỏng bằng từ khóa · không gọi AI thật' : `DeepSeek · ${job.model}`}</p>}
        {job.status === 'failed' && <p>Tác vụ chưa thành công. File CV vẫn có thể tải về; hệ thống không tạo điểm đánh giá thay thế.</p>}
        {output?.skills && <><p>Kỹ năng được nhắc trực tiếp trong tài liệu:</p><ul>{output.skills.map((s, i) => <li key={i}><strong>{s.name}</strong> — “{s.quote}” <small>{s.evidenceRef}</small></li>)}</ul>{!output.skills.length && <p>Chưa tìm thấy kỹ năng trong tài liệu.</p>}</>}
        {latestRevision?.schemaVersion === 1 && <section className="revision-review" aria-label="Rà soát dữ liệu trích xuất">
          <h3>Rà soát dữ liệu trích xuất</h3>
          <p>Phiên bản rà soát {latestRevision.revisionNo} · {latestRevision.origin === 'ai_extracted' ? 'Chờ bạn xác nhận' : 'Bạn đã xác nhận'}</p>
          <details><summary>Thông tin kỹ thuật</summary><small>Bộ phân tích: {latestRevision.parserVersion}</small></details>
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
            <button type="button" disabled={busy} onClick={() => void action(async () => {
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
        <button type="button" className="secondary" disabled={busy} onClick={() => void action(async () => { setPollCount(c => c + 1); setJob(await api<Job>(`/api/ai/jobs/${job.id}`)); })}>Kiểm tra lại</button>
      </>}</section></div><FullCvReview versionId={selectedResumeVersionId} refreshKey={`${job?.id}-${job?.status}`} parentPolling={!!job && job.resumeVersionId === selectedResumeVersionId && job.schemaVersion === 'cv-fields-v3' && ['queued', 'running'].includes(job.status)}/></PersistentView></> : null}
      {session.roles.includes('Recruiter') && <><PersistentView active={recruiterViews.includes(activeView)} name={activeView}><section className="card"><h2>Tài khoản nhà tuyển dụng</h2><p>Recruiter được mở file gốc khi ứng viên chủ động công khai hoặc từ snapshot của đơn thuộc tin mình phụ trách.</p></section><RecruiterJobManager activeView={activeView}/></PersistentView><PersistentView active={activeView === 'shared-profile'} name="shared-profile"><RecruiterProfileLookup active={activeView === 'shared-profile'}/></PersistentView></>}{session.roles.includes('Admin')&&<PersistentView active={activeView === 'administration'} name="administration"><AdminPanel/></PersistentView>}<PersistentView active={activeView === 'account'} name="account"><div id="account"><AccountSettingsPanel session={session} onSessionUpdated={patch => setSession(prev => prev ? { ...prev, ...patch } : prev)} deletionSlot={session.roles.some(role => role === 'Candidate' || role === 'Recruiter') ? <AccountDeletionPanel onPending={purgeAfter => setAuthenticated({ authenticated: true, roles: [], deletionPending: true, purgeAfter })}/> : undefined}/></div></PersistentView>

    </>}
    <footer className="workspace-footer"><span>CareerGap AI<span className="footer-dot" aria-hidden="true">·</span>Hồ sơ rõ ràng. Cơ hội phù hợp.</span><span>AI hỗ trợ. Bạn quyết định.</span></footer>
  </Workspace>;
}
createRoot(document.getElementById('root')!).render(<React.StrictMode><MotionRoot><App/></MotionRoot></React.StrictMode>);
