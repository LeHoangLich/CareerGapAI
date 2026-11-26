import { BusinessIdentityPanel } from './businessIdentity';
import { LocationInput } from './locationInput';
import { JobTagPicker } from './jobTags';
import { SkillPicker } from './skillPicker';
import { CompanyLogo, CompanyLogoEditor } from './companyLogo';
import { salaryLabel, salaryPayload, salaryPeriods } from './salary';
import { Alert, ConfirmDialog, Dialog, EmptyState, Pagination, StaticLoading, ValidatedForm } from './ui';
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { api, ApiError, type PagedResponse } from './api';
import { jobHref, jobIdFromHash, jobShareUrl, jobSignInHref } from './jobLinks';
import { coverLetterLimit, normalizeCoverLetter } from './coverLetter';
import { RecruiterKeywordResumeSearch } from './matching';
import { JobDiscoveryWorkspace } from './jobDiscoveryWorkspace';
import { PairAnalysisButton, PairRecommendations } from './pairMatching';
import { ReportButton } from './administration';
import { Briefcase, MagnifyingGlass, MapPin, SlidersHorizontal, ArrowRight } from '@phosphor-icons/react';
import { PersistentView } from './persistentView';
import type { WorkspaceView } from './navigation';
import {RecruitmentNotifications,NotificationApplicationDetail} from './recruitmentNotifications';

type Session = { authenticated: boolean; userId?: string; roles: string[] };
type Company = { logoUrl?: string | null; id: string; name: string; slug: string; description: string | null; websiteUrl: string | null; location: string | null; industry: string | null; sizeLabel: string | null; rowVersion: number };
type JobVersion = { id: string; versionNo: number; title: string; description: string; requirementsText: string; benefitsText: string | null; industry: string | null; location: string | null; employmentType: string; workMode: string; experienceMin: number | null; experienceMax: number | null; salaryMin: number | null; salaryMax: number | null; salaryCurrency: string | null; salaryPeriod: string | null; language: string; contentHash: string };
type JobRequirement = { minimumYears?: number | null; relation?: string; alternatives?: string[]; needsReview?: boolean; requirementId: string; text: string; importance: string; sourceType: string };
type JobRequirementEvidence = { requirementId: string; sourceType: string; startOffset: number | null; endOffset: number | null; quote: string | null; statement: string | null };
type JobDataRevision = { id: string; jobVersionId: string; revisionNo: number; parentRevisionId: string | null; origin: string; structuredData: { schemaVersion: number; requirements: JobRequirement[] }; sourceText: { requirementsText: string }; evidence: JobRequirementEvidence[]; dataHash: string; parserVersion: string; confirmedBy: string | null; confirmedAt: string | null; createdAt: string };
type Job = { tags?: string[]; id: string; status: string; companyId: string; company: Company; version: JobVersion; applicationDeadline: string | null; acceptingApplications: boolean; rowVersion: number; publishedAt: string | null; closedAt: string | null; maxApplications?: number | null; activeApplicationCount?: number; remainingSlots?: number | null; archivedByRole?: string | null; viewCount?: number };
type Page<T> = { items: T[]; page: number; pageSize: number; total: number };
type Saved = { savedAt: string; job: Job };
type Resume = { id: string; title: string; versions: { id: string; versionNo: number; createdAt: string }[] };
type Application = { id: string; jobId: string; jobTitle: string; companyName: string; status: string; submittedAt: string; statusChangedAt: string; resumeVersionNo: number; rowVersion: number; candidateId: string; candidateBlocked: boolean; coverLetter: string | null };
type ApplicationHistory = { fromStatus: string | null; toStatus: string; changedAt: string };
type ApplicationNote = { id: string; body: string; createdAt: string };
type SearchFilters = { tags: string; tagMode: string; q: string; skill: string; industry: string; location: string; workMode: string; employmentType: string; experienceYears: string };
type JobForm = { title: string; description: string; requirementsText: string; benefitsText: string; industry: string; location: string; employmentType: string; workMode: string; experienceMin: string; experienceMax: string; language: string; salaryMin:string;salaryMax:string;salaryCurrency:string;salaryPeriod:string };

const emptyFilters: SearchFilters = { tags: '', tagMode: 'all', q: '', skill: '', industry: '', location: '', workMode: '', employmentType: '', experienceYears: '' };
const emptyJob: JobForm = { title: '', description: '', requirementsText: '', benefitsText: '', industry: '', location: '', employmentType: 'full_time', workMode: 'unspecified', experienceMin: '', experienceMax: '', language: 'vi', salaryMin:'',salaryMax:'',salaryCurrency:'VND',salaryPeriod:'month' };
const activeApplicationStatuses = ['submitted', 'reviewing', 'shortlisted'];
const statusLabels: Record<string, string> = { submitted: 'Đã nộp', reviewing: 'Đang xem xét', shortlisted: 'Được chọn tiếp', rejected: 'Không tiếp tục', withdrawn: 'Đã rút' };
const recruiterTransitions: Record<string, string[]> = { submitted: ['reviewing', 'shortlisted', 'rejected'], reviewing: ['shortlisted', 'rejected'], shortlisted: ['reviewing', 'rejected'] };

function statusLabel(status: string) { return statusLabels[status] ?? status; }
const importanceLabels: Record<string, string> = { required: 'Bắt buộc', optional: 'Tùy chọn', unspecified: 'Chưa xác định' };
const employmentLabels: Record<string, string> = { full_time: 'Toàn thời gian', part_time: 'Bán thời gian', contract: 'Hợp đồng', internship: 'Thực tập', temporary: 'Tạm thời' };
const workModeLabels: Record<string, string> = { onsite: 'Tại văn phòng', hybrid: 'Linh hoạt', remote: 'Từ xa', unspecified: 'Chưa xác định' };

function toQuery(filters: SearchFilters, page: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: '10' });
  for (const [key, value] of Object.entries(filters)) if (value.trim()) params.set(key, value.trim());
  return params.toString();
}

function parseJobsHash(hash: string): { filters: SearchFilters; page: number } | null {
  const [route, query = ''] = hash.split('?');
  if (route !== '#jobs') return null;
  const p = new URLSearchParams(query);
  const rawPage = Number(p.get('page') ?? '1');
  const page = Number.isInteger(rawPage) && rawPage >= 1 && rawPage <= 10000 ? rawPage : 1;
  return {
    filters: {
      q: p.get('q') ?? '',
      location: p.get('location') ?? '',
      tags: p.get('tags') ?? '',
      tagMode: p.get('tagMode') === 'any' ? 'any' : 'all',
      skill: p.get('skill') ?? '',
      industry: p.get('industry') ?? '',
      workMode: p.get('workMode') ?? '',
      employmentType: p.get('employmentType') ?? '',
      experienceYears: p.get('experienceYears') ?? '',
    },
    page,
  };
}

function buildJobsHash(filters: SearchFilters, page: number): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filters)) {
    const trimmed = value.trim();
    if (!trimmed) continue;
    if (key === 'tagMode' && trimmed === 'all' && !filters.tags.trim()) continue;
    params.set(key, trimmed);
  }
  if (page > 1) params.set('page', String(page));
  const qs = params.toString();
  return qs ? `#jobs?${qs}` : '#jobs';
}

function ShareJobLink({ jobId }: { jobId: string }) {
  const [message,setMessage]=useState('');
  const url=jobShareUrl(jobId,window.location.href);
  return <section className="job-share" aria-label="Chia sẻ việc làm">
    <label>Liên kết chia sẻ việc làm<input readOnly value={url} onFocus={event=>event.currentTarget.select()}/></label>
    <button type="button" className="secondary" onClick={()=>{
      if(!navigator.clipboard){setMessage('Hãy chọn liên kết và sao chép thủ công.');return;}
      void navigator.clipboard.writeText(url).then(()=>setMessage('Đã sao chép liên kết.'),()=>setMessage('Hãy chọn liên kết và sao chép thủ công.'));
    }}>Sao chép liên kết</button>
    <p role="status">{message}</p>
  </section>;
}

function SubmittedCoverLetter({ value }: { value: string | null }) {
  return value?<details className="application-cover-letter"><summary>Thư ứng tuyển đã gửi</summary><p>{value}</p></details>
    :<small>Không gửi thư ứng tuyển.</small>;
}

function JobCard({ job, candidate, reportable = false, saved, onToggleSaved, resumes, application, selectedVersionId, onSelectVersion, onApply, applying, detail=false, saving=false, coverLetter, onCoverLetter }: {
  job: Job; candidate: boolean; reportable?: boolean; saved: boolean; onToggleSaved: (job: Job) => void;
  resumes: Resume[]; application?: Application; selectedVersionId?: string;
  onSelectVersion: (jobId: string, versionId: string) => void; onApply: (job: Job) => void; applying: boolean; detail?: boolean; saving?: boolean;
  coverLetter: string; onCoverLetter: (jobId: string, value: string) => void;
}) {
  const version = job.version;
  const resumeVersions = resumes.flatMap(resume => resume.versions.map(item => ({ ...item, resumeTitle: resume.title })));
  return <article className="job-card" data-job-id={job.id}>
    <div className="job-title-row"><div className="job-identity"><CompanyLogo name={job.company.name} url={job.company.logoUrl}/><div>{detail?<h1>{version.title}</h1>:<h3><a href={jobHref(job.id)}>{version.title}</a></h3>}<p className="company-name">{job.company.name}</p></div></div><span className={`job-status ${job.status}`}>{job.status === 'published' ? job.acceptingApplications ? 'Đang nhận hồ sơ' : job.remainingSlots === 0 ? 'Đã đủ hồ sơ' : 'Đã hết hạn' : 'Đã đóng'}</span></div>
    {!!job.tags?.length&&<div className="row job-tags" aria-label="Tag tin tuyển dụng">{job.tags.map(tag=><span className="badge" key={tag}>{tag}</span>)}</div>}
    <div className="job-meta"><span><MapPin size={14} aria-hidden="true"/>{version.location || job.company.location || 'Chưa có địa điểm'}</span><span>{employmentLabels[version.employmentType] ?? version.employmentType}</span><span>{workModeLabels[version.workMode] ?? version.workMode}</span><span>{version.industry || 'Chưa phân ngành'}</span><span>{version.experienceMin ?? '—'}–{version.experienceMax ?? '—'} năm</span><span>{(job.viewCount ?? 0).toLocaleString('vi-VN')} lượt xem</span></div>
    <p className="job-salary"><strong>{salaryLabel(version)}</strong></p>
    {detail&&job.publishedAt&&<small>Đăng ngày {new Date(job.publishedAt).toLocaleDateString('vi-VN')}</small>}
    {detail?<div className="job-detail-body">
      <section><h2>Mô tả công việc</h2><p>{version.description}</p></section>
      <section><h2>Yêu cầu công việc</h2><p>{version.requirementsText}</p></section>
      {version.benefitsText&&<section><h2>Quyền lợi</h2><p>{version.benefitsText}</p></section>}
      <section><h2>Về {job.company.name}</h2>{job.company.description&&<p>{job.company.description}</p>}
        <dl className="job-company-facts"><div><dt>Địa điểm</dt><dd>{job.company.location||'Chưa cập nhật'}</dd></div><div><dt>Ngành</dt><dd>{job.company.industry||'Chưa cập nhật'}</dd></div><div><dt>Quy mô</dt><dd>{job.company.sizeLabel||'Chưa cập nhật'}</dd></div></dl>
        {job.company.websiteUrl?.startsWith('https://')&&<a href={job.company.websiteUrl} target="_blank" rel="noopener noreferrer">Website doanh nghiệp (mở tab mới)</a>}
      </section>
    </div>:<p className="job-description">{version.description.length > 220 ? `${version.description.slice(0, 220)}…` : version.description}</p>}
    {job.applicationDeadline && <small>Hạn nhận hồ sơ: {new Date(job.applicationDeadline).toLocaleString('vi-VN')}</small>}
    {job.maxApplications != null && <small className="job-capacity">{job.remainingSlots === 0 ? `Đã đủ ${job.maxApplications}/${job.maxApplications} hồ sơ đang xử lý` : `Còn ${job.remainingSlots ?? job.maxApplications}/${job.maxApplications} chỗ nhận hồ sơ`}</small>}
    <div className="row">
      {!detail && <a className="button-link" href={jobHref(job.id)}>{candidate && !application && job.acceptingApplications ? 'Xem chi tiết & Ứng tuyển' : 'Xem chi tiết việc làm'}</a>}
      {candidate && <button type="button" className="secondary save-job" disabled={saving} aria-pressed={saved} onClick={() => onToggleSaved(job)}>{saving?'Đang lưu…':saved ? 'Bỏ lưu việc' : 'Lưu việc'}</button>}
      {(candidate || reportable) && <ReportButton targetType="job" targetId={job.id}/>}
    </div>
    {candidate && application && <p className="hint" role="status">Bạn đã nộp hồ sơ · {statusLabel(application.status)} · CV phiên bản {application.resumeVersionNo}</p>}
    {detail && candidate && application && <SubmittedCoverLetter value={application.coverLetter}/>}
    {detail && candidate && !application && job.acceptingApplications && <div className="apply-row">
      {resumeVersions.length ? <><label>CV gửi<select aria-label={`CV gửi cho ${version.title}`} disabled={applying} value={selectedVersionId ?? resumeVersions[0].id} onChange={e => onSelectVersion(job.id, e.target.value)}>
        {resumeVersions.map(item => <option key={item.id} value={item.id}>{item.resumeTitle} · v{item.versionNo}</option>)}
      </select></label><label className="cover-letter-input">Thư ứng tuyển (không bắt buộc)<textarea aria-label={`Thư ứng tuyển cho ${version.title}`} rows={5} maxLength={coverLetterLimit} disabled={applying} value={coverLetter} onChange={event=>onCoverLetter(job.id,event.target.value)} placeholder="Giới thiệu ngắn về bạn và lý do quan tâm đến vị trí này."/><small>{coverLetter.length.toLocaleString('vi-VN')} / 5.000 ký tự · Thư được gửi cùng CV và không thể sửa sau khi nộp.</small></label><button type="button" disabled={applying} onClick={() => onApply(job)}>{applying ? 'Đang nộp…' : 'Nộp hồ sơ'}</button></>
        : <p className="hint"><a href="#private-resumes">Tải CV lên mục “CV riêng của bạn”</a> để nộp hồ sơ.</p>}
    </div>}
    {detail&&!job.acceptingApplications&&<p className="hint">{job.status==='closed'?'Tin đã đóng, không nhận thêm hồ sơ.':job.remainingSlots===0?'Tin đã nhận đủ số hồ sơ đang xử lý.':'Tin đã hết hạn nhận hồ sơ.'}</p>}
    {detail&&<ShareJobLink jobId={job.id}/>}
  </article>;
}

export function PublicJobBrowser({ session, activeView, intro }: { session?: Session; activeView: WorkspaceView; intro?: ReactNode }) {
  const [detailId,setDetailId]=useState(()=>jobIdFromHash(window.location.hash));
  const [detailRetry,setDetailRetry]=useState(0);
  const [detail,setDetail]=useState<{id:string;job?:Job;application?:Application;saved:boolean;error?:string;missing?:boolean}>();
  const [savingJobId,setSavingJobId]=useState<string>();
  const [filters, setFilters] = useState<SearchFilters>(() => parseJobsHash(window.location.hash)?.filters ?? emptyFilters);
  const [appliedFilters, setAppliedFilters] = useState<SearchFilters>(() => parseJobsHash(window.location.hash)?.filters ?? emptyFilters);
  const [page, setPage] = useState(() => parseJobsHash(window.location.hash)?.page ?? 1);
  const [results, setResults] = useState<Page<Job>>();
  const [saved, setSaved] = useState<Saved[]>([]);
  const [resumes, setResumes] = useState<Resume[]>([]);
  const [applications, setApplications] = useState<Application[]>([]);
  const [applicationHistory, setApplicationHistory] = useState<Record<string, ApplicationHistory[]>>({});
  const [expandedHistory, setExpandedHistory] = useState<Record<string, boolean>>({});
  const [withdrawal, setWithdrawal] = useState<Application>();
  const [withdrawingId, setWithdrawingId] = useState<string>();
  const [notificationRevision,setNotificationRevision]=useState(0);
  const [selectedVersionByJob, setSelectedVersionByJob] = useState<Record<string, string>>({});
  const [coverLettersByJob,setCoverLettersByJob]=useState<Record<string,string>>({});
  const [applyingJobId, setApplyingJobId] = useState<string>();
  const [applicationMessage, setApplicationMessage] = useState('');
  const [applicationDataState, setApplicationDataState] = useState<'loading' | 'ready' | 'error'>('loading');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const advancedFilters = useRef<HTMLDetailsElement>(null);
  const submissionKeys = useRef(new Map<string, string>());
  const candidate = session?.authenticated === true && session.roles.includes('Candidate');
  const currentDetail=detail?.id===detailId?detail:undefined;

  useEffect(()=>{
    const update=()=>{
      if(window.location.hash==='#main-content')return;
      setDetailId(jobIdFromHash(window.location.hash));
      if(window.location.hash.startsWith('#jobs?')){
        const parsed=parseJobsHash(window.location.hash);
        if(parsed){
          setFilters(parsed.filters);
          setAppliedFilters(parsed.filters);
          setPage(parsed.page);
        }
      }
    };
    update();
    window.addEventListener('hashchange',update);
    return()=>window.removeEventListener('hashchange',update);
  },[]);
  useEffect(()=>{
    if(activeView!=='job'||!detailId)return;
    const controller=new AbortController();
    setDetail(undefined);setError('');setApplicationMessage('');
    void Promise.all([
      api<Job>(`/api/jobs/${detailId}`,{signal:controller.signal}),
      candidate?api<Page<Application>>(`/api/applications/mine?jobId=${detailId}&pageSize=1`,{signal:controller.signal}):Promise.resolve(undefined),
      candidate?api<Page<Saved>>(`/api/saved-jobs?jobId=${detailId}&pageSize=1`,{signal:controller.signal}):Promise.resolve(undefined)
    ]).then(([job,ownApplications,ownSaved])=>{
      if(!controller.signal.aborted)setDetail({id:detailId,job,application:ownApplications?.items.find(x=>activeApplicationStatuses.includes(x.status)),saved:!!ownSaved?.total});
    }).catch(e=>{
      if(!controller.signal.aborted)setDetail({id:detailId,saved:false,error:(e as Error).message,missing:e instanceof ApiError&&e.status===404});
    });
    return()=>controller.abort();
  },[activeView,detailId,candidate,detailRetry]);
  useEffect(()=>{
    if(activeView!=='job'||!currentDetail?.job)return;
    const previous=document.title;
    document.title=`${currentDetail.job.version.title} · ${currentDetail.job.company.name} | CareerGap AI`;
    return()=>{document.title=previous;};
  },[activeView,currentDetail?.job]);

  const refreshSaved = useCallback(async () => {
    if (!candidate) { setSaved([]); return; }
    try { setSaved((await api<Page<Saved>>('/api/saved-jobs?page=1&pageSize=50')).items); }
    catch (e) { setError((e as Error).message); }
  }, [candidate]);

  useEffect(() => {
    let active = true;
    setBusy(true); setError('');
    api<Page<Job>>(`/api/jobs?${toQuery(appliedFilters, page)}`).then(value => { if (active) setResults(value); })
      .catch(e => { if (active) setError((e as Error).message); })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [appliedFilters, page]);

  useEffect(() => { void refreshSaved(); }, [refreshSaved]);
  const refreshApplicationData = useCallback(async () => {
    if (!session?.authenticated) { setResumes([]); setApplications([]); return; }
    setApplicationDataState('loading');
    try {
      if (candidate) {
        const [resumePage, applicationPage] = await Promise.all([
          api<PagedResponse<Resume>>('/api/resumes?page=1'), api<Page<Application>>('/api/applications/mine?page=1&pageSize=50')
        ]);
        const resumeList = resumePage.items;
        setResumes(resumeList); setApplications(applicationPage.items);
        setSelectedVersionByJob(current => {
          const valid = new Set(resumeList.flatMap(resume => resume.versions.map(version => version.id)));
          const next = Object.fromEntries(Object.entries(current).filter(([, versionId]) => valid.has(versionId)));
          return next;
        });
      } else {
        setResumes([]); setApplications([]);
      }
      setApplicationDataState('ready');
    } catch (e) { setApplicationDataState('error'); setError((e as Error).message); }
  }, [candidate, session?.authenticated]);
  useEffect(() => {
    const refresh = () => { void refreshApplicationData(); };
    refresh();
    window.addEventListener('resume-list-updated', refresh);
    return () => window.removeEventListener('resume-list-updated', refresh);
  }, [refreshApplicationData]);
  const savedIds = useMemo(() => new Set(saved.map(x => x.job.id)), [saved]);
  const activeApplications = useMemo(() => new Map(applications.filter(x => activeApplicationStatuses.includes(x.status)).map(x => [x.jobId, x])), [applications]);
  async function toggleSaved(job: Job, isSaved=savedIds.has(job.id)) {
    setError('');
    setSavingJobId(job.id);
    try {
      await api(`/api/saved-jobs/${job.id}`, { method: isSaved ? 'DELETE' : 'PUT' });
      setDetail(current=>current?.id===job.id?{...current,saved:!isSaved}:current);
      await refreshSaved();
    } catch (e) { setError((e as Error).message); }
    finally {setSavingJobId(undefined);}
  }
  async function apply(job: Job) {
    const versions = resumes.flatMap(resume => resume.versions);
    const resumeVersionId = selectedVersionByJob[job.id] ?? versions[0]?.id;
    if (!resumeVersionId) return;
    let coverLetter: string | null;
    try {coverLetter=normalizeCoverLetter(coverLettersByJob[job.id]??'');}
    catch(e){setError((e as Error).message);return;}
    const keyRef = JSON.stringify([job.id,resumeVersionId,coverLetter]);
    const key = submissionKeys.current.get(keyRef) ?? crypto.randomUUID();
    submissionKeys.current.set(keyRef, key);
    setApplyingJobId(job.id); setError(''); setApplicationMessage('');
    try {
      const application = await api<Application>(`/api/jobs/${job.id}/applications`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resumeVersionId, submissionKey: key, coverLetter }) });
      setCoverLettersByJob(current=>{const next={...current};delete next[job.id];return next;});
      setDetail(current=>current?.id===job.id?{...current,application}:current);
      setApplicationMessage(`Đã nộp hồ sơ cho “${application.jobTitle}”.`);
      await refreshApplicationData(); setNotificationRevision(value=>value+1);
    } catch (e) { setError((e as Error).message); }
    finally { setApplyingJobId(undefined); }
  }
  async function withdraw(application: Application) {
    setWithdrawingId(application.id); setError(''); setApplicationMessage('');
    try {
      await api(`/api/applications/${application.id}/withdraw`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion: application.rowVersion }) });
      setApplicationMessage(`Bạn đã rút hồ sơ cho “${application.jobTitle}”.`);
      setWithdrawal(undefined);
      await refreshApplicationData(); setNotificationRevision(value=>value+1);
    } catch (e) { setError((e as Error).message); }
    finally { setWithdrawingId(undefined); }
  }
  async function toggleHistory(application: Application) {
    const open = !expandedHistory[application.id];
    setExpandedHistory(current => ({ ...current, [application.id]: open }));
    if (open && !applicationHistory[application.id]) {
      try {
        const history = await api<ApplicationHistory[]>(`/api/applications/${application.id}/history`);
        setApplicationHistory(current => ({ ...current, [application.id]: history }));
      }
      catch (e) { setError((e as Error).message); }
    }
  }
  const items = results?.items ?? [];
  const jobResults = <>
    {error && <Alert tone="error">{error}</Alert>}
    {applicationMessage && <Alert tone="success">{applicationMessage}</Alert>}
    {busy && <StaticLoading label="Đang tìm tin…"/>}
    {!busy && !items.length && !error && <EmptyState icon={<Briefcase size={28} aria-hidden="true"/>} title="Chưa có tin công khai phù hợp." description="Thử thay đổi từ khóa hoặc mở rộng bộ lọc để tìm cơ hội khác."/>}
    <div className="job-list">{items.map(job => <JobCard key={job.id} coverLetter={coverLettersByJob[job.id]??''} onCoverLetter={(jobId,value)=>setCoverLettersByJob(current=>({...current,[jobId]:value}))} job={job} candidate={candidate} reportable={session?.roles.includes('Recruiter')} saved={savedIds.has(job.id)} onToggleSaved={jobToSave => void toggleSaved(jobToSave)} resumes={resumes} application={activeApplications.get(job.id)} selectedVersionId={selectedVersionByJob[job.id]} onSelectVersion={(jobId, versionId) => setSelectedVersionByJob(current => ({ ...current, [jobId]: versionId }))} onApply={jobToApply => void apply(jobToApply)} applying={applyingJobId === job.id}/>)}</div>
    {results && <Pagination page={page} totalItems={results.total} pageSize={results.pageSize} busy={busy} ariaLabel="Phân trang việc làm" onPageChange={nextPage => { setPage(nextPage); const nextHash = buildJobsHash(appliedFilters, nextPage); if (window.location.hash !== nextHash) window.location.hash = nextHash; }}/>}
    {candidate && saved.length > 0 && <details className="saved-jobs"><summary>Việc đã lưu ({saved.length})</summary><div className="job-list">{saved.map(x => <JobCard key={x.job.id} coverLetter={coverLettersByJob[x.job.id]??''} onCoverLetter={(jobId,value)=>setCoverLettersByJob(current=>({...current,[jobId]:value}))} job={x.job} candidate={true} saved={true} onToggleSaved={jobToSave => void toggleSaved(jobToSave)} resumes={resumes} application={activeApplications.get(x.job.id)} selectedVersionId={selectedVersionByJob[x.job.id]} onSelectVersion={(jobId, versionId) => setSelectedVersionByJob(current => ({ ...current, [jobId]: versionId }))} onApply={jobToApply => void apply(jobToApply)} applying={applyingJobId === x.job.id}/>)}</div></details>}
    {!candidate && <p className="hint">Đăng nhập bằng tài khoản ứng viên để lưu tin quan tâm.</p>}
  </>;
  const detailResumeVersions = resumes.flatMap(resume => resume.versions.map(item => ({ ...item, resumeTitle: resume.title })));
  return <><PersistentView active={activeView === 'jobs'} name="jobs">{intro}<section className="card job-browser" id="jobs">
    <div className="section-heading"><div><p className="eyebrow">KHÁM PHÁ CƠ HỘI</p><h1>Tìm công việc</h1></div><span className="result-count">{results ? `${results.total} kết quả` : busy ? 'Đang tải…' : 'Chưa có kết quả'}</span></div>
    <ValidatedForm className="job-filters job-search-bar" onInvalidCapture={event => {
      if (advancedFilters.current?.contains(event.target as Node)) advancedFilters.current.open = true;
    }} onSubmit={e => {
      e.preventDefault();
      const next = { ...filters };
      setPage(1);
      setAppliedFilters(next);
      const nextHash = buildJobsHash(next, 1);
      if (window.location.hash !== nextHash) window.location.hash = nextHash;
    }}>
      <label>Từ khóa<input value={filters.q} onChange={e => setFilters({ ...filters, q: e.target.value })} maxLength={120} placeholder="Vị trí hoặc công ty bạn quan tâm"/></label>
      <label>Địa điểm<LocationInput value={filters.location} onChange={location=>setFilters({...filters,location})} placeholder="Tỉnh, thành phố"/></label>
      <button type="submit" disabled={busy}><MagnifyingGlass size={17} aria-hidden="true"/>Tìm việc</button>
      <details className="advanced-job-filters" ref={advancedFilters}>
        <summary><SlidersHorizontal size={16} aria-hidden="true"/>Bộ lọc nâng cao<span className="filter-count">{[appliedFilters.tags, appliedFilters.skill, appliedFilters.industry, appliedFilters.workMode, appliedFilters.employmentType, appliedFilters.experienceYears].filter(value => value.trim()).length} đang áp dụng</span></summary>
        <div className="advanced-filter-grid">
      <JobTagPicker label="Chọn tag để lọc việc làm" tags={filters.tags?filters.tags.split(','):[]} onChange={tags=>setFilters({...filters,tags:tags.join(',')})}/><label>Cách khớp tag<select value={filters.tagMode} onChange={e=>setFilters({...filters,tagMode:e.target.value})}><option value="all">Khớp tất cả tag</option><option value="any">Khớp ít nhất một tag</option></select></label>
      <label>Kỹ năng<input value={filters.skill} onChange={e => setFilters({ ...filters, skill: e.target.value })} maxLength={100} placeholder="Ví dụ: PostgreSQL"/></label>
      <label>Ngành<input value={filters.industry} onChange={e => setFilters({ ...filters, industry: e.target.value })} maxLength={100} placeholder="Tất cả ngành nghề"/></label>
      <label>Hình thức<select value={filters.workMode} onChange={e => setFilters({ ...filters, workMode: e.target.value })}><option value="">Tất cả</option><option value="onsite">Tại văn phòng</option><option value="hybrid">Linh hoạt</option><option value="remote">Từ xa</option><option value="unspecified">Chưa xác định</option></select></label>
      <label>Loại công việc<select value={filters.employmentType} onChange={e => setFilters({ ...filters, employmentType: e.target.value })}><option value="">Tất cả</option><option value="full_time">Toàn thời gian</option><option value="part_time">Bán thời gian</option><option value="contract">Hợp đồng</option><option value="internship">Thực tập</option><option value="temporary">Tạm thời</option></select></label>
      <label>Kinh nghiệm của bạn (năm)<input type="number" min="0" max="99.99" step="0.01" value={filters.experienceYears} onChange={e => setFilters({ ...filters, experienceYears: e.target.value })}/></label>
        </div>
      </details>
    </ValidatedForm>
    {candidate ? <JobDiscoveryWorkspace resumes={resumes}>{jobResults}</JobDiscoveryWorkspace> : jobResults}
  </section></PersistentView><PersistentView active={activeView==='notifications'&&session?.authenticated===true} name="notifications">{session?.authenticated&&<RecruitmentNotifications key={session.userId} userId={session.userId} active={activeView==='notifications'} revision={notificationRevision}/>}</PersistentView><PersistentView active={activeView==='application'&&session?.authenticated===true} name="application"><NotificationApplicationDetail active={activeView==='application'&&session?.authenticated===true}/></PersistentView><PersistentView active={activeView==='job'} name="job">
    <section className="job-detail" aria-label="Chi tiết việc làm">
      <a className="job-detail-back" href={buildJobsHash(appliedFilters, page)}>← Quay lại danh sách việc làm</a>
      {!detailId||currentDetail?.missing?<div className="card empty-state"><h1>Không tìm thấy tin tuyển dụng</h1><p>Tin không tồn tại hoặc không còn công khai. Hãy quay lại danh sách để xem những cơ hội khác.</p></div>
        :currentDetail?.error?<div className="card" role="alert"><h1>Chưa tải được tin tuyển dụng</h1><p>{currentDetail.error}</p><button type="button" className="secondary" onClick={()=>setDetailRetry(value=>value+1)}>Tải lại tin tuyển dụng</button></div>
        :!currentDetail?.job?<StaticLoading label="Đang tải chi tiết việc làm…"/>:<>
          {error&&<p role="alert" className="error">{error}</p>}{applicationMessage&&<p role="status">{applicationMessage}</p>}
          <JobCard key={currentDetail.job.id} coverLetter={coverLettersByJob[currentDetail.job.id]??''} onCoverLetter={(jobId,value)=>setCoverLettersByJob(current=>({...current,[jobId]:value}))} detail job={currentDetail.job} candidate={candidate&&applicationDataState==='ready'} reportable={session?.roles.includes('Recruiter')} saved={currentDetail.saved} saving={savingJobId===currentDetail.job.id} onToggleSaved={job=>void toggleSaved(job,currentDetail.saved)} resumes={resumes} application={currentDetail.application} selectedVersionId={selectedVersionByJob[currentDetail.job.id]} onSelectVersion={(jobId,versionId)=>setSelectedVersionByJob(current=>({...current,[jobId]:versionId}))} onApply={job=>void apply(job)} applying={applyingJobId===currentDetail.job.id}/>
          {candidate&&applicationDataState==='ready'&&<section className="card job-pair-match" aria-label="Đối chiếu CV với tin tuyển dụng">
            <div className="section-heading"><div><p className="eyebrow">PHÂN TÍCH PHÙ HỢP CÓ BẰNG CHỨNG</p><h2>Đối chiếu CV của bạn với tin tuyển dụng này</h2></div><span className="badge">CV–JD</span></div>
            {detailResumeVersions.length ? <>
              <label>Chọn phiên bản CV để đối chiếu
                <select value={selectedVersionByJob[currentDetail.job.id] ?? detailResumeVersions[0].id} onChange={e => { const detailJobId = currentDetail.job?.id; const versionId = e.target.value; if (detailJobId) setSelectedVersionByJob(current => ({ ...current, [detailJobId]: versionId })); }}>
                  {detailResumeVersions.map(item => <option key={item.id} value={item.id}>{item.resumeTitle} · v{item.versionNo}</option>)}
                </select>
              </label>
              <PairAnalysisButton
                resumeVersionId={selectedVersionByJob[currentDetail.job.id] ?? detailResumeVersions[0].id}
                jobVersionId={currentDetail.job.version.id}
                jobId={currentDetail.job.id}
              />
            </> : <p className="hint"><a href="#private-resumes">Tải hoặc tạo CV riêng</a> để đối chiếu với tin tuyển dụng này.</p>}
          </section>}
          {!session?.authenticated&&currentDetail.job.acceptingApplications&&<div className="card job-detail-signin"><p>Đăng nhập bằng tài khoản ứng viên để chọn CV, lưu tin và ứng tuyển.</p><a href={jobSignInHref(currentDetail.job.id)}>Đăng nhập để ứng tuyển</a></div>}
          {session?.authenticated&&!candidate&&<p className="hint">Chỉ tài khoản ứng viên được nộp hồ sơ ứng tuyển.</p>}
          {candidate&&applicationDataState==='loading'&&<StaticLoading label="Đang tải CV và trạng thái ứng tuyển…"/>}
          {candidate&&applicationDataState==='error'&&<div role="alert"><p>Chưa tải đủ CV và trạng thái ứng tuyển.</p><button type="button" className="secondary" onClick={()=>void refreshApplicationData()}>Tải lại dữ liệu ứng tuyển</button></div>}
        </>}
    </section>
  </PersistentView><PersistentView active={activeView === 'applications' && candidate} name="applications">
    {error && <p role="alert" className="error">{error}</p>}
    {applicationDataState === 'loading' && <StaticLoading label="Đang tải đơn ứng tuyển…"/>}
    {applicationDataState === 'error' && <div className="application-data-error"><p>Chưa tải đủ đơn ứng tuyển. Bạn có thể kiểm tra lại.</p><button type="button" className="secondary" onClick={() => { setError(''); void refreshApplicationData(); }}>Tải lại đơn ứng tuyển</button></div>}
    <Dialog open={!!withdrawal} title="Rút hồ sơ ứng tuyển?" busy={!!withdrawingId} onClose={() => setWithdrawal(undefined)}>
      <p>Bạn đang rút hồ sơ cho “{withdrawal?.jobTitle}”. Nhà tuyển dụng sẽ thấy trạng thái đã rút. Nếu ứng tuyển lại, bạn cần thực hiện một lượt nộp mới theo điều kiện của tin.</p>
      {error && <p role="alert" className="error">{error}</p>}
      <div className="dialog-actions"><button type="button" className="secondary" disabled={!!withdrawingId} onClick={() => setWithdrawal(undefined)}>Giữ hồ sơ</button>
        <button type="button" className="danger" disabled={!!withdrawingId} onClick={() => { if (withdrawal) void withdraw(withdrawal); }}>{withdrawingId ? 'Đang rút…' : 'Xác nhận rút hồ sơ'}</button></div>
    </Dialog>
    <p><a href="#notifications">Mở trung tâm thông báo tuyển dụng</a></p><div className="application-workspace" aria-busy={applicationDataState === 'loading'}>
    {candidate && <section className="card application-center" id="applications"><div className="section-heading"><div><p className="eyebrow">HỒ SƠ ĐÃ NỘP</p><h2>Đơn ứng tuyển của bạn</h2></div><span>{applicationDataState === 'ready' ? applications.length : '—'}</span></div>
      {applicationMessage && <p role="status">{applicationMessage}</p>}
      {!applications.length ? applicationDataState === 'ready' && <div className="empty-state application-empty"><Briefcase size={32} aria-hidden="true"/><h3>Cơ hội tiếp theo đang chờ bạn</h3><p>Chưa có đơn ứng tuyển. Khám phá việc phù hợp và chọn phiên bản CV bạn muốn gửi.</p><a className="empty-state-cta" href="#jobs">Khám phá việc làm ngay<ArrowRight size={16} aria-hidden="true"/></a></div> : <ul className="application-list">{applications.map(application => <li key={application.id}><div><strong><a href={jobHref(application.jobId)}>{application.jobTitle}</a></strong><small>{application.companyName} · {new Date(application.submittedAt).toLocaleString('vi-VN')} · {statusLabel(application.status)}</small><SubmittedCoverLetter value={application.coverLetter}/><div className="row application-actions"><a href={`/api/applications/${application.id}/resume`}>CV đã nộp · v{application.resumeVersionNo}</a>{activeApplicationStatuses.includes(application.status) && <button type="button" className="secondary" disabled={withdrawingId === application.id} onClick={() => { setError(''); setWithdrawal(application); }}>{withdrawingId === application.id ? 'Đang rút…' : 'Rút hồ sơ'}</button>}<button type="button" className="secondary" aria-expanded={!!expandedHistory[application.id]} aria-controls={`application-history-${application.id}`} onClick={() => void toggleHistory(application)}>{expandedHistory[application.id] ? 'Ẩn lịch sử' : 'Lịch sử trạng thái'}</button></div><ul hidden={!expandedHistory[application.id]} id={`application-history-${application.id}`} className="status-history">{(applicationHistory[application.id] ?? []).map((entry, index) => <li key={`${entry.changedAt}-${index}`}><span>{entry.fromStatus ? `${statusLabel(entry.fromStatus)} → ` : ''}{statusLabel(entry.toStatus)}</span><small>{new Date(entry.changedAt).toLocaleString('vi-VN')}</small></li>)}</ul></div></li>)}</ul>}
    </section>}

    </div>
  </PersistentView></>;
}

function JobRequirementReview({ job }: { job: Job }) {
  const basePath = `/api/recruiter/jobs/${job.id}/versions/${job.version.id}/revisions`;
  const [revisions, setRevisions] = useState<JobDataRevision[]>([]);
  const [included, setIncluded] = useState<Record<string, { checked: boolean; importance: string }>>({});
  const [manualText, setManualText] = useState('');
  const [manualImportance, setManualImportance] = useState('unspecified');
  const [manualRequirements, setManualRequirements] = useState<{ text: string; importance: string }[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const latest = revisions[0];

  const refresh = useCallback(async () => {
    const value = await api<JobDataRevision[]>(basePath);
    setRevisions(value);
  }, [basePath]);

  useEffect(() => { void refresh().catch(e => setError((e as Error).message)); }, [refresh]);
  useEffect(() => {
    if (!latest || latest.origin !== 'local_extracted') return;
    setIncluded(Object.fromEntries(latest.structuredData.requirements.map(item => [item.requirementId,
      { checked: true, importance: item.importance }])));
  }, [latest?.id]);

  async function extract() {
    setBusy(true); setError('');
    try {
      const revision = await api<JobDataRevision>(`${basePath}/extract`, { method: 'POST' });
      setRevisions(current => [revision, ...current.filter(item => item.id !== revision.id)]);
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }

  async function confirm() {
    if (!latest) return;
    setBusy(true); setError('');
    try {
      const revision = await api<JobDataRevision>(`${basePath}/${latest.id}/confirm`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          baseRevisionNo: latest.revisionNo,
          acceptedRequirements: latest.structuredData.requirements.filter(item => included[item.requirementId]?.checked)
            .map(item => ({ requirementId: item.requirementId, importance: included[item.requirementId]?.importance ?? item.importance })),
          userStatements: manualRequirements
        })
      });
      setRevisions(current => [revision, ...current.filter(item => item.id !== revision.id)]);
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }

  function addManualRequirement() {
    const text = manualText.trim();
    if (!text) return;
    setManualRequirements(current => [...current, { text, importance: manualImportance }]);
    setManualText('');
  }

  const evidenceById = new Map((latest?.evidence ?? []).map(item => [item.requirementId, item]));
  return <section className="job-revision-review" aria-label={`Rà soát yêu cầu JD phiên bản ${job.version.versionNo}`}>
    <div><strong>Yêu cầu có cấu trúc · JD v{job.version.versionNo}</strong>
      <p>Mỗi dòng giữ nguyên câu nguồn. Phân loại tự động chỉ là gợi ý từ từ khóa; hãy rà soát trước khi xác nhận. Thao tác này chạy local, không gọi AI.</p></div>
    {error && <p role="alert" className="error">{error}</p>}
    {!latest && <button type="button" className="secondary" disabled={busy} onClick={() => void extract()}>{busy ? 'Đang tách dòng…' : 'Tách yêu cầu từ JD (local)'}</button>}
    {latest && <>
      <p>Revision {latest.revisionNo} · {latest.origin === 'local_extracted' ? 'Chờ recruiter rà soát' : 'Đã xác nhận'} · {latest.parserVersion}</p>
      {!latest.structuredData.requirements.length && <p>Không có dòng yêu cầu để phân loại; recruiter có thể bổ sung bên dưới.</p>}
      <ul className="job-requirement-list">{latest.structuredData.requirements.map(item => {
        const evidence = evidenceById.get(item.requirementId);
        const editable = latest.origin === 'local_extracted';
        const state = included[item.requirementId] ?? { checked: true, importance: item.importance };
        return <li key={item.requirementId}>
          {editable ? <label><input type="checkbox" aria-label={`Giữ yêu cầu: ${item.text}`} checked={state.checked}
            onChange={event => setIncluded(current => ({ ...current, [item.requirementId]: { ...state, checked: event.target.checked } }))}/>{item.text}</label>
            : <strong>{item.text}</strong>}
          {editable ? <select aria-label={`Phân loại yêu cầu: ${item.text}`} value={state.importance}
            onChange={event => setIncluded(current => ({ ...current, [item.requirementId]: { ...state, importance: event.target.value } }))}>
            {Object.entries(importanceLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select> : <span>{importanceLabels[item.importance] ?? item.importance}</span>}
          {evidence?.quote && <small>Nguồn JD · ký tự {evidence.startOffset}–{evidence.endOffset}: “{evidence.quote}”</small>}
          {item.minimumYears != null && <small>Kinh nghiệm nêu trong nguồn: tối thiểu {item.minimumYears} năm.</small>}
          {item.relation === 'any_of' && <small>Một trong các lựa chọn: {item.alternatives?.join(' | ')}. Không yêu cầu đồng thời tất cả.</small>}
          {item.needsReview && <small>Cần rà soát cách tách/quan hệ; nếu chưa đúng, bỏ mục này và bổ sung yêu cầu đã sửa bên dưới.</small>}
          {evidence?.sourceType === 'user_statement' && <small>Recruiter bổ sung · không phải trích dẫn từ JD: “{evidence.statement}”</small>}
        </li>;
      })}</ul>
      {latest.origin === 'local_extracted' && <>
        <div className="job-manual-requirement"><label>Yêu cầu recruiter bổ sung<input aria-label="Yêu cầu recruiter bổ sung" maxLength={2000} value={manualText} onChange={event => setManualText(event.target.value)}/></label>
          <label>Phân loại<select aria-label="Phân loại yêu cầu bổ sung" value={manualImportance} onChange={event => setManualImportance(event.target.value)}>
            {Object.entries(importanceLabels).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select></label><button type="button" className="secondary" disabled={!manualText.trim()} onClick={addManualRequirement}>Thêm yêu cầu</button></div>
        {!!manualRequirements.length && <ul className="job-manual-list">{manualRequirements.map((item, index) => <li key={`${item.text}-${index}`}><span>{item.text} · {importanceLabels[item.importance]}</span><button type="button" className="link-button" onClick={() => setManualRequirements(current => current.filter((_, i) => i !== index))}>Bỏ</button></li>)}</ul>}
        <button type="button" disabled={busy} onClick={() => void confirm()}>{busy ? 'Đang lưu…' : 'Xác nhận yêu cầu JD'}</button>
      </>}
    </>}
  </section>;
}

export function RecruiterJobManager({ activeView }: { activeView: WorkspaceView }) {
  const [business,setBusiness]=useState<{companyId:string;status:string}|null>(null);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [jobQuery,setJobQuery]=useState({page:1,status:''});
  const [jobPage,setJobPage]=useState<Page<Job>&{statusFilter:string}>();
  const [jobsLoading,setJobsLoading]=useState(false);
  const [jobsError,setJobsError]=useState('');
  const jobsRequest=useRef(0);
  const currentJobPage=jobPage?.page===jobQuery.page&&jobPage.statusFilter===jobQuery.status?jobPage:undefined;
  const jobs=currentJobPage?.items??[];
  const jobsPageSize=10;
  const [applicationsByJob, setApplicationsByJob] = useState<Record<string, Page<Application> | undefined>>({});
  const [applicationQueries,setApplicationQueries]=useState<Record<string,{page:number;status:string}>>({});
  const applicationQueriesRef=useRef(applicationQueries);
  const applicationRequests=useRef<Record<string,AbortController>>({});
  const [applicationErrors,setApplicationErrors]=useState<Record<string,string>>({});
  const applicationsPageSize=10;
  const [expandedApplications, setExpandedApplications] = useState<Record<string, boolean>>({});
  const [expandedNotes, setExpandedNotes] = useState<Record<string, boolean>>({});
  const [notesByApplication, setNotesByApplication] = useState<Record<string, ApplicationNote[]>>({});
  const [noteDrafts, setNoteDrafts] = useState<Record<string, string>>({});
  const [mutatingApplicationId, setMutatingApplicationId] = useState<string>();
  const [savingNoteId, setSavingNoteId] = useState<string>();
  const [loadingApplications, setLoadingApplications] = useState<Record<string,boolean>>({});
  const [companyForm, setCompanyForm] = useState({ name: '', description: '', websiteUrl: '', location: '', industry: '', sizeLabel: '' });
  const [editingCompany, setEditingCompany] = useState<Company>();
  const [companyId, setCompanyId] = useState('');
  const [jobTags,setJobTags]=useState<string[]>([]);
  const [jobForm, setJobForm] = useState<JobForm>(emptyJob);
  const [deadline, setDeadline] = useState('');
  const [maxApplications, setMaxApplications] = useState('');
  const [editingJob, setEditingJob] = useState<Job>();
  const [composerOpen, setComposerOpen] = useState(false);
  const [archiveTarget, setArchiveTarget] = useState<Job | null>(null);
  const [rewriteProposal, setRewriteProposal] = useState<{ id: string; status: string; errorCode?: string | null; provider?: string; sections?: { description: string; requirementsText: string; benefitsText: string | null } | null }>();
  const [rewriteBusy, setRewriteBusy] = useState(false);
  const rewriteAbortRef = useRef<AbortController | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  const refresh = useCallback(async (signal?:AbortSignal) => {
    const ticket=++jobsRequest.current;
    setJobsLoading(true);setJobsError('');
    try {
      const params=new URLSearchParams({page:String(jobQuery.page),pageSize:String(jobsPageSize)});
      if(jobQuery.status)params.set('status',jobQuery.status);
      const [companyList,result,binding]=await Promise.all([
        api<Company[]>('/api/companies/mine',{signal}),
        api<Page<Job>>(`/api/jobs/mine?${params}`,{signal}),
        api<{identity:{companyId:string;status:string}|null}>('/api/recruiter/business',{signal})
      ]);
      if(signal?.aborted||ticket!==jobsRequest.current)return;
      setCompanies(companyList);setBusiness(binding.identity);
      setCompanyId(binding.identity?.companyId??'');
      const lastPage=Math.max(1,Math.ceil(result.total/jobsPageSize));
      if(jobQuery.page>lastPage){setJobQuery(current=>({...current,page:lastPage}));return;}
      setJobPage({...result,statusFilter:jobQuery.status});
    }catch(e){
      if(signal?.aborted||ticket!==jobsRequest.current)return;
      setJobPage(undefined);setJobsError((e as Error).message);
    }finally{if(ticket===jobsRequest.current)setJobsLoading(false);}
  },[jobQuery]);

  useEffect(()=>{
    const controller=new AbortController();void refresh(controller.signal);
    return()=>{jobsRequest.current++;controller.abort();};
  },[refresh]);

  useEffect(()=>{
    const requests=applicationRequests.current;
    return()=>{
      rewriteAbortRef.current?.abort();
      Object.values(requests).forEach(controller=>controller.abort());
    };
  },[]);

  const jobPagination=(position:'trên'|'dưới')=><nav className="row" aria-label={`Phân trang tin tuyển dụng phía ${position}`}>
    <button type="button" className="secondary" disabled={busy||jobsLoading||jobQuery.page<=1} onClick={()=>setJobQuery(current=>({...current,page:current.page-1}))}>Trang tin trước</button>
    <span>Trang {jobQuery.page} / {currentJobPage?Math.max(1,Math.ceil(currentJobPage.total/jobsPageSize)):'…'}</span>
    <button type="button" className="secondary" disabled={busy||jobsLoading||!currentJobPage||jobQuery.page*jobsPageSize>=currentJobPage.total} onClick={()=>setJobQuery(current=>({...current,page:current.page+1}))}>Trang tin sau</button>
  </nav>;

  async function saveCompany(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError(''); setMessage('');
    try {
      const savedCompany = editingCompany
        ? await api<Company>(`/api/companies/${editingCompany.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ...companyForm, rowVersion: editingCompany.rowVersion }) })
        : await api<Company>('/api/companies', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(companyForm) });
      setEditingCompany(undefined); setCompanyForm({ name: '', description: '', websiteUrl: '', location: '', industry: '', sizeLabel: '' });
      await refresh(); setCompanyId(savedCompany.id); setMessage('Hồ sơ công ty đã được lưu.');
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  async function saveJob(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError(''); setMessage('');
    let content;
    try { content = { ...jobForm, experienceMin: jobForm.experienceMin === '' ? null : Number(jobForm.experienceMin), experienceMax: jobForm.experienceMax === '' ? null : Number(jobForm.experienceMax), ...salaryPayload(jobForm) }; } catch(e) { setError((e as Error).message);setBusy(false);return; }
    const applicationDeadline = deadline ? new Date(deadline).toISOString() : null;
    const parsedCapacity = maxApplications.trim() === '' ? null : Number(maxApplications.trim());
    if (parsedCapacity !== null && (!Number.isInteger(parsedCapacity) || parsedCapacity < 1 || parsedCapacity > 10000)) {
      setError('Giới hạn hồ sơ đang xử lý phải là số nguyên từ 1 đến 10.000.'); setBusy(false); return;
    }
    try {
      if (editingJob) await api<Job>(`/api/jobs/${editingJob.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion: editingJob.rowVersion, applicationDeadline, content, tags:jobTags, maxApplications: parsedCapacity }) });
      else await api<Job>('/api/jobs', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ companyId, applicationDeadline, content, tags:jobTags, maxApplications: parsedCapacity }) });
      setEditingJob(undefined); setJobTags([]); setJobForm(emptyJob); setDeadline(''); setMaxApplications(''); await refresh(); setMessage('JD đã được lưu thành phiên bản mới.');
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  function editJob(job: Job) {
    rewriteAbortRef.current?.abort();
    setRewriteBusy(false);
    setEditingJob(job); setRewriteProposal(undefined); setJobTags(job.tags??[]); setCompanyId(job.companyId); setMaxApplications(job.maxApplications?.toString() ?? '');
    setJobForm({ title: job.version.title, description: job.version.description, requirementsText: job.version.requirementsText, benefitsText: job.version.benefitsText ?? '', industry: job.version.industry ?? '', location: job.version.location ?? '', employmentType: job.version.employmentType, workMode: job.version.workMode, experienceMin: job.version.experienceMin?.toString() ?? '', experienceMax: job.version.experienceMax?.toString() ?? '', language: job.version.language, salaryMin:job.version.salaryMin?.toString()??'',salaryMax:job.version.salaryMax?.toString()??'',salaryCurrency:job.version.salaryCurrency??'VND',salaryPeriod:job.version.salaryPeriod??'month' });
    if (job.applicationDeadline) {
      const date = new Date(job.applicationDeadline);
      setDeadline(new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16));
    } else setDeadline('');
  }

  async function requestJdRewrite() {
    if (!editingJob) return;
    rewriteAbortRef.current?.abort();
    const controller = new AbortController();
    rewriteAbortRef.current = controller;
    setRewriteBusy(true); setError(''); setRewriteProposal(undefined);
    try {
      const created = await api<{ id: string }>(`/api/jobs/${editingJob.id}/rewrite-proposals`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ idempotencyKey: crypto.randomUUID() }), signal: controller.signal
      });
      for (let i = 0; i < 60; i++) {
        if (controller.signal.aborted) return;
        const value = await api<NonNullable<typeof rewriteProposal>>(`/api/writing/jd-proposals/${created.id}`, { signal: controller.signal });
        if (controller.signal.aborted) return;
        setRewriteProposal(value);
        if (value.status !== 'queued' && value.status !== 'running') break;
        await new Promise<void>((resolve, reject) => {
          const timer = setTimeout(() => { controller.signal.removeEventListener('abort', onAbort); resolve(); }, 1500);
          function onAbort() { clearTimeout(timer); reject(new DOMException('Aborted', 'AbortError')); }
          controller.signal.addEventListener('abort', onAbort, { once: true });
        });
        if (i === 59) setError('Tác vụ viết lại JD đang mất nhiều thời gian hơn dự kiến. Bạn có thể bấm thử lại sau.');
      }
    } catch (e) {
      if (!(e instanceof Error && e.name === 'AbortError')) setError((e as Error).message);
    } finally {
      if (rewriteAbortRef.current === controller) {
        rewriteAbortRef.current = null;
        setRewriteBusy(false);
      }
    }
  }

  async function changeState(job: Job, action: 'publish' | 'close' | 'archive' | 'restore') {
    setBusy(true); setError(''); setMessage('');
    try {
      await api<Job>(`/api/jobs/${job.id}/${action}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion: job.rowVersion }) });
      setArchiveTarget(null);
      await refresh();
      setMessage(action === 'publish' ? 'Tin đã được công khai.' : action === 'close' ? 'Tin đã được đóng.' : action === 'archive' ? 'Tin đã được ẩn khỏi danh sách công khai.' : 'Tin đã được khôi phục.');
    }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  async function toggleApplications(job: Job) {
    if (expandedApplications[job.id]) {
      applicationRequests.current[job.id]?.abort();
      setLoadingApplications(current=>({...current,[job.id]:false}));
      setExpandedApplications(current => ({ ...current, [job.id]: false })); return;
    }
    setExpandedApplications(current => ({ ...current, [job.id]: true }));
    await refreshJobApplications(job.id);
  }

  async function refreshJobApplications(jobId: string, query=applicationQueriesRef.current[jobId]??{page:1,status:''}) {
    applicationRequests.current[jobId]?.abort();
    const controller=new AbortController();applicationRequests.current[jobId]=controller;
    applicationQueriesRef.current={...applicationQueriesRef.current,[jobId]:query};
    setApplicationQueries(applicationQueriesRef.current);
    setApplicationsByJob(current=>({...current,[jobId]:undefined}));
    setLoadingApplications(current=>({...current,[jobId]:true}));
    setApplicationErrors(current=>({...current,[jobId]:''}));
    try {
      const params=new URLSearchParams({page:String(query.page),pageSize:String(applicationsPageSize)});
      if(query.status)params.set('status',query.status);
      const result=await api<Page<Application>>(`/api/jobs/${jobId}/applications?${params}`,{signal:controller.signal});
      if(controller.signal.aborted||applicationRequests.current[jobId]!==controller)return;
      const lastPage=Math.max(1,Math.ceil(result.total/applicationsPageSize));
      if(query.page>lastPage){await refreshJobApplications(jobId,{...query,page:lastPage});return;}
      setApplicationsByJob(current=>({...current,[jobId]:result}));
    }catch(e){
      if(!controller.signal.aborted&&applicationRequests.current[jobId]===controller)
        setApplicationErrors(current=>({...current,[jobId]:(e as Error).message}));
    }finally{
      if(!controller.signal.aborted&&applicationRequests.current[jobId]===controller)
        setLoadingApplications(current=>({...current,[jobId]:false}));
    }
  }

  const applicationPagination=(jobId:string,position:'trên'|'dưới')=>{
    const query=applicationQueries[jobId]??{page:1,status:''};
    const result=applicationsByJob[jobId];
    return <nav className="row" aria-label={`Phân trang hồ sơ phía ${position}`}>
      <button type="button" className="secondary" disabled={!!mutatingApplicationId||loadingApplications[jobId]||query.page<=1} onClick={()=>void refreshJobApplications(jobId,{...query,page:query.page-1})}>Trang hồ sơ trước</button>
      <span>Trang {query.page} / {result?Math.max(1,Math.ceil(result.total/applicationsPageSize)):'…'}</span>
      <button type="button" className="secondary" disabled={!!mutatingApplicationId||loadingApplications[jobId]||!result||query.page*applicationsPageSize>=result.total} onClick={()=>void refreshJobApplications(jobId,{...query,page:query.page+1})}>Trang hồ sơ sau</button>
    </nav>;
  };

  async function changeApplicationStatus(jobId: string, application: Application, status: string) {
    setMutatingApplicationId(application.id); setError(''); setMessage('');
    try {
      await api<Application>(`/api/applications/${application.id}/status`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion: application.rowVersion, status }) });
      await refreshJobApplications(jobId); setMessage(`Trạng thái hồ sơ đã chuyển thành “${statusLabel(status)}”.`);
    } catch (e) { setError((e as Error).message); }
    finally { setMutatingApplicationId(undefined); }
  }

  async function toggleCandidateBlock(jobId: string, application: Application) {
    setMutatingApplicationId(application.id); setError(''); setMessage('');
    try {
      await api(`/api/jobs/${jobId}/candidates/${application.candidateId}/block`, { method: application.candidateBlocked ? 'DELETE' : 'PUT' });
      await refreshJobApplications(jobId); setMessage(application.candidateBlocked ? 'Đã bỏ chặn ứng viên cho tin này.' : 'Đã chặn ứng viên cho tin này.');
    } catch (e) { setError((e as Error).message); }
    finally { setMutatingApplicationId(undefined); }
  }

  async function toggleNotes(application: Application) {
    const open = !expandedNotes[application.id];
    setExpandedNotes(current => ({ ...current, [application.id]: open }));
    if (open && !Object.prototype.hasOwnProperty.call(notesByApplication, application.id)) {
      try {
        const notes = await api<PagedResponse<ApplicationNote>>(`/api/applications/${application.id}/notes`);
        setNotesByApplication(current => ({ ...current, [application.id]: notes.items }));
      } catch (e) { setError((e as Error).message); }
    }
  }

  async function addApplicationNote(application: Application) {
    const body = noteDrafts[application.id]?.trim();
    if (!body) return;
    setSavingNoteId(application.id); setError(''); setMessage('');
    try {
      await api<ApplicationNote>(`/api/applications/${application.id}/notes`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ body }) });
      const notes = await api<PagedResponse<ApplicationNote>>(`/api/applications/${application.id}/notes`);
      setNotesByApplication(current => ({ ...current, [application.id]: notes.items }));
      setNoteDrafts(current => ({ ...current, [application.id]: '' })); setMessage('Đã thêm ghi chú nội bộ.');
    } catch (e) { setError((e as Error).message); }
    finally { setSavingNoteId(undefined); }
  }

  return <section className="card recruiter-jobs" id="recruiter-jobs">
    {error && <Alert tone="error">{error}</Alert>}{message && <Alert tone="success">{message}</Alert>}
    {business?.status!=='verified'&&<p className="hint">Để công khai tin, hãy vào <a href="#recruiter-company">Hồ sơ công ty</a>, gắn công ty chính và hoàn tất xác minh doanh nghiệp.</p>}
    <div hidden={activeView !== 'recruiter-company'}>
    <BusinessIdentityPanel companies={companies} onChange={refresh}/>
    <div className="company-row"><strong>Công ty của tôi</strong>{companies.map(company => <div className="company-chip" key={company.id}><span>{company.name}</span><button type="button" className="link-button" disabled={business?.companyId!==company.id} onClick={() => { setEditingCompany(company); setCompanyForm({ name: company.name, description: company.description ?? '', websiteUrl: company.websiteUrl ?? '', location: company.location ?? '', industry: company.industry ?? '', sizeLabel: company.sizeLabel ?? '' }); }}>Sửa</button></div>)}</div>
    {companies.filter(company=>company.id===business?.companyId).map(company=><CompanyLogoEditor key={company.id} company={company} onUpdated={refresh}/>)}
    {!editingCompany&&companies.length>0&&business?.status!=='verified'&&<p className="hint">Tài khoản đã có hồ sơ doanh nghiệp. Hãy chọn công ty chính ở trên (nếu chưa chọn), rồi nhấn Sửa để cập nhật; không tạo thêm công ty.</p>}
    {(editingCompany || business?.status!=='verified') && <ValidatedForm className="editor-form" onSubmit={saveCompany}><h3>{editingCompany ? 'Cập nhật công ty' : 'Tạo hồ sơ công ty'}</h3>
      <label>Tên công ty<input required maxLength={200} value={companyForm.name} onChange={e => setCompanyForm({ ...companyForm, name: e.target.value })}/></label>
      <label>Giới thiệu<textarea maxLength={4000} value={companyForm.description} onChange={e => setCompanyForm({ ...companyForm, description: e.target.value })}/></label>
      <div className="form-columns"><label>Website HTTPS<input type="url" placeholder="https://example.com" value={companyForm.websiteUrl} onChange={e => setCompanyForm({ ...companyForm, websiteUrl: e.target.value })}/></label><label>Địa điểm<LocationInput value={companyForm.location} onChange={location=>setCompanyForm({...companyForm,location})}/></label><label>Ngành<input maxLength={100} value={companyForm.industry} onChange={e => setCompanyForm({ ...companyForm, industry: e.target.value })}/></label><label>Quy mô<input maxLength={50} placeholder="11-50" value={companyForm.sizeLabel} onChange={e => setCompanyForm({ ...companyForm, sizeLabel: e.target.value })}/></label></div>
      <div className="row"><button type="submit" disabled={busy||(!editingCompany&&companies.length>0)}>{busy ? 'Đang lưu…' : editingCompany ? 'Lưu công ty' : 'Tạo công ty'}</button>{editingCompany && <button type="button" className="secondary" onClick={() => { setEditingCompany(undefined); setCompanyForm({ name: '', description: '', websiteUrl: '', location: '', industry: '', sizeLabel: '' }); }}>Hủy sửa</button>}</div>
    </ValidatedForm>}
    </div>
    <div hidden={activeView !== 'recruiter-jobs'}>
    {!composerOpen && !editingJob && <div className="row" style={{ marginBottom: 16 }}><button type="button" onClick={() => setComposerOpen(true)}>+ Tạo tin tuyển dụng mới</button></div>}
    {(composerOpen || editingJob) && <ValidatedForm className="editor-form" onSubmit={saveJob}><div className="section-heading"><h3>{editingJob ? `Sửa JD · bản mới từ v${editingJob.version.versionNo}` : 'Tạo tin nháp'}</h3><button type="button" className="secondary" onClick={() => { rewriteAbortRef.current?.abort(); setRewriteBusy(false); setComposerOpen(false); setEditingJob(undefined); setRewriteProposal(undefined); setJobTags([]); setJobForm(emptyJob); setDeadline(''); setMaxApplications(''); }}>Đóng biểu mẫu</button></div>
      {!editingJob && <label>Công ty<select required value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Chọn công ty</option>{companies.filter(c=>c.id===business?.companyId).map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>}
      <label>Chức danh<input required maxLength={200} value={jobForm.title} onChange={e => setJobForm({ ...jobForm, title: e.target.value })}/></label>
      <label>Mô tả công việc<textarea required maxLength={12000} value={jobForm.description} onChange={e => setJobForm({ ...jobForm, description: e.target.value })}/></label>
      <label>Yêu cầu và kỹ năng<textarea required maxLength={12000} value={jobForm.requirementsText} onChange={e => setJobForm({ ...jobForm, requirementsText: e.target.value })}/></label>
      <SkillPicker label="Chọn kỹ năng chuẩn cho JD" values={jobForm.requirementsText.split('\n').map(x=>x.trim()).filter(Boolean)} max={100} disabled={busy||jobForm.requirementsText.length>11800} onAdd={name=>setJobForm(current=>({...current,requirementsText:current.requirementsText+(current.requirementsText&&!current.requirementsText.endsWith('\n')?'\n':'')+name}))}/>
      <JobTagPicker label="Chọn tag cho tin tuyển dụng" tags={jobTags} onChange={setJobTags} disabled={busy}/><p className="hint">Tag dùng phân loại và lọc tin; yêu cầu kỹ năng của JD được quản lý riêng ở ô bên trên.</p>
      <label>Quyền lợi<textarea maxLength={5000} value={jobForm.benefitsText} onChange={e => setJobForm({ ...jobForm, benefitsText: e.target.value })}/></label>
      <fieldset><legend>Mức lương</legend><p className="hint">Để trống cả hai mức nếu chưa công bố lương.</p><div className="form-columns"><label>Lương tối thiểu<input type="number" min="0" step="0.01" value={jobForm.salaryMin} onChange={e=>setJobForm({...jobForm,salaryMin:e.target.value})}/></label><label>Lương tối đa<input type="number" min="0" step="0.01" value={jobForm.salaryMax} onChange={e=>setJobForm({...jobForm,salaryMax:e.target.value})}/></label><label>Tiền tệ<input maxLength={3} pattern="[A-Za-z]{3}" list="salary-currencies" value={jobForm.salaryCurrency} onChange={e=>setJobForm({...jobForm,salaryCurrency:e.target.value.toUpperCase()})}/><datalist id="salary-currencies"><option value="VND"/><option value="USD"/></datalist></label><label>Kỳ trả lương<select value={jobForm.salaryPeriod} onChange={e=>setJobForm({...jobForm,salaryPeriod:e.target.value})}>{Object.entries(salaryPeriods).map(([v,label])=><option key={v} value={v}>{label}</option>)}</select></label></div></fieldset>
      <div className="form-columns"><label>Ngành<input maxLength={100} value={jobForm.industry} onChange={e => setJobForm({ ...jobForm, industry: e.target.value })}/></label><label>Địa điểm<LocationInput value={jobForm.location} onChange={location=>setJobForm({...jobForm,location})}/></label><label>Loại công việc<select value={jobForm.employmentType} onChange={e => setJobForm({ ...jobForm, employmentType: e.target.value })}><option value="full_time">Toàn thời gian</option><option value="part_time">Bán thời gian</option><option value="contract">Hợp đồng</option><option value="internship">Thực tập</option><option value="temporary">Tạm thời</option></select></label><label>Hình thức<select value={jobForm.workMode} onChange={e => setJobForm({ ...jobForm, workMode: e.target.value })}><option value="unspecified">Chưa xác định</option><option value="onsite">Tại văn phòng</option><option value="hybrid">Linh hoạt</option><option value="remote">Từ xa</option></select></label><label>Kinh nghiệm tối thiểu (năm)<input type="number" min="0" max="99.99" step="0.01" value={jobForm.experienceMin} onChange={e => setJobForm({ ...jobForm, experienceMin: e.target.value })}/></label><label>Kinh nghiệm tối đa (năm)<input type="number" min="0" max="99.99" step="0.01" value={jobForm.experienceMax} onChange={e => setJobForm({ ...jobForm, experienceMax: e.target.value })}/></label><label>Ngôn ngữ JD<select value={jobForm.language} onChange={e => setJobForm({ ...jobForm, language: e.target.value })}><option value="vi">Tiếng Việt</option><option value="en">English</option><option value="vi-en">Việt–Anh</option></select></label><label>Hạn nhận hồ sơ<input type="datetime-local" value={deadline} onChange={e => setDeadline(e.target.value)}/></label><label>Giới hạn hồ sơ đang xử lý<input type="number" min="1" max="10000" step="1" placeholder="Không giới hạn" value={maxApplications} onChange={e => setMaxApplications(e.target.value)}/></label></div>
      <div className="row"><button type="submit" disabled={busy || (!editingJob && !companyId)}>{busy ? 'Đang lưu…' : editingJob ? 'Lưu phiên bản mới' : 'Tạo tin nháp'}</button>{editingJob && <button type="button" className="secondary" disabled={busy || rewriteBusy} onClick={() => void requestJdRewrite()}>{rewriteBusy ? 'Đang tạo gợi ý AI…' : 'AI gợi ý viết lại JD'}</button>}{editingJob && <button type="button" className="secondary" onClick={() => { rewriteAbortRef.current?.abort(); setRewriteBusy(false); setEditingJob(undefined); setRewriteProposal(undefined); setJobTags([]); setJobForm(emptyJob); setDeadline(''); setMaxApplications(''); }}>Hủy sửa</button>}</div>
      {rewriteProposal?.status === 'succeeded' && rewriteProposal.sections && <section className="card" aria-label="Đề xuất viết lại JD">
        <h4>Đề xuất viết lại JD ({rewriteProposal.provider === 'Fake' ? 'AI mô phỏng' : 'AI'})</h4>
        <p><strong>Mô tả đề xuất:</strong></p><pre style={{ whiteSpace: 'pre-wrap' }}>{rewriteProposal.sections.description}</pre>
        <p><strong>Yêu cầu đề xuất:</strong></p><pre style={{ whiteSpace: 'pre-wrap' }}>{rewriteProposal.sections.requirementsText}</pre>
        {rewriteProposal.sections.benefitsText && <><p><strong>Quyền lợi đề xuất:</strong></p><pre style={{ whiteSpace: 'pre-wrap' }}>{rewriteProposal.sections.benefitsText}</pre></>}
        <button type="button" className="secondary" onClick={() => { setJobForm(current => ({ ...current, description: rewriteProposal.sections!.description, requirementsText: rewriteProposal.sections!.requirementsText, benefitsText: rewriteProposal.sections!.benefitsText ?? current.benefitsText })); setMessage('Đã áp dụng đề xuất vào bản nháp. Hãy kiểm tra lại rồi bấm Lưu phiên bản mới.'); }}>Áp dụng vào bản nháp</button>
      </section>}
      {rewriteProposal && (rewriteProposal.status === 'failed' || rewriteProposal.status === 'cancelled') && <Alert tone="error">Không tạo được gợi ý viết lại JD ({rewriteProposal.errorCode ?? 'lỗi xử lý'}).</Alert>}
    </ValidatedForm>}
    </div>
    <div hidden={activeView === 'recruiter-company'}>
    <h3>{activeView === 'recruiter-applicants' ? 'Ứng viên theo tin tuyển dụng' : 'Tin của tôi'}</h3>
    <div className="row recruiter-job-filters"><label>Lọc trạng thái tin<select aria-label="Lọc trạng thái tin" value={jobQuery.status} disabled={busy} onChange={e=>setJobQuery({page:1,status:e.target.value})}>
      <option value="">Tất cả trạng thái</option><option value="draft">Nháp</option><option value="published">Đã đăng</option><option value="closed">Đã đóng</option><option value="archived">Đã ẩn</option>
    </select></label><p role="status" aria-atomic="true">{jobsLoading?'Đang tải danh sách tin…':currentJobPage?`${currentJobPage.total} tin · ${jobs.length?((jobQuery.page-1)*jobsPageSize+1):0}–${(jobQuery.page-1)*jobsPageSize+jobs.length}`:''}</p></div>
    {jobsError&&<div role="alert"><p>{jobsError}</p><button type="button" className="secondary" disabled={jobsLoading} onClick={()=>void refresh()}>Tải lại danh sách tin</button></div>}
    {jobPagination('trên')}
    {!jobsLoading&&currentJobPage&&!jobs.length&&<p>{jobQuery.status?'Không có tin nào khớp trạng thái đã chọn.':'Chưa có tin. Hãy tạo công ty rồi bắt đầu bằng một tin nháp.'}</p>}
    <div className="job-list" aria-busy={jobsLoading}>{jobs.map(job => {
      const applicationPage=applicationsByJob[job.id];
      return <article className="job-card" key={job.id}><div className="job-title-row"><div><h3>{['published','closed'].includes(job.status)?<a href={jobHref(job.id)}>{job.version.title}</a>:job.version.title}</h3><p>{job.company.name} · phiên bản {job.version.versionNo} · {(job.viewCount ?? 0).toLocaleString('vi-VN')} lượt xem{job.maxApplications != null ? ` · ${job.activeApplicationCount ?? 0}/${job.maxApplications} hồ sơ đang xử lý` : ''}</p></div><span className={`job-status ${job.status}`}>{({draft:'Bản nháp',published:'Đang đăng',closed:'Đã đóng',archived:'Đã ẩn'} as Record<string,string>)[job.status]??job.status}</span></div><p>{job.version.description}</p><p className="job-salary"><strong>{salaryLabel(job.version)}</strong></p>{job.status === 'archived' && <p className="hint">{job.archivedByRole === 'recruiter' ? 'Tin do bạn tự ẩn. Bạn có thể khôi phục thành tin đã đóng hoặc bản nháp.' : 'Tin đã bị quản trị viên ẩn.'}</p>}<div className="row" hidden={activeView !== 'recruiter-jobs'}><button type="button" className="secondary" disabled={busy || business?.companyId!==job.companyId || ['closed','archived'].includes(job.status)} onClick={() => editJob(job)}>Sửa JD</button>{job.status === 'draft' && <button type="button" disabled={busy||business?.status!=='verified'||business.companyId!==job.companyId} onClick={() => void changeState(job, 'publish')}>Công khai</button>}{job.status === 'published' && <button type="button" className="secondary" disabled={busy||business?.companyId!==job.companyId} onClick={() => void changeState(job, 'close')}>Đóng tin</button>}{job.status !== 'archived' && <button type="button" className="secondary" disabled={busy||business?.companyId!==job.companyId} onClick={() => setArchiveTarget(job)}>Ẩn tin</button>}{job.status === 'archived' && job.archivedByRole === 'recruiter' && <button type="button" className="secondary" disabled={busy||business?.companyId!==job.companyId} onClick={() => void changeState(job, 'restore')}>Khôi phục tin</button>}<a href="#recruiter-applicants">Xem ứng viên</a></div>
      <details hidden={activeView !== 'recruiter-jobs'||business?.companyId!==job.companyId} className="recruiter-job-tools"><summary>Rà soát yêu cầu JD &amp; Tìm ứng viên phù hợp</summary><JobRequirementReview key={`${job.id}:${job.version.id}`} job={job}/>
      <RecruiterKeywordResumeSearch jobId={job.id} enabled={job.status === 'published' && job.acceptingApplications}/>
      </details>
      <div hidden={activeView !== 'recruiter-applicants'}><button type="button" className="secondary" onClick={() => void toggleApplications(job)}>{expandedApplications[job.id] ? 'Ẩn hồ sơ' : 'Hồ sơ ứng tuyển'}</button>
      <PairRecommendations key={`applications:${job.id}`} direction="applications" jobId={job.id}/>
      {expandedApplications[job.id] && <div className="job-applications" aria-busy={!!loadingApplications[job.id]}><h4>Hồ sơ ứng tuyển</h4>
        <div className="row"><label>Lọc trạng thái hồ sơ<select aria-label="Lọc trạng thái hồ sơ" value={applicationQueries[job.id]?.status??''} disabled={!!mutatingApplicationId} onChange={e=>void refreshJobApplications(job.id,{page:1,status:e.target.value})}>
          <option value="">Tất cả trạng thái</option>{Object.entries(statusLabels).map(([value,label])=><option key={value} value={value}>{label}</option>)}
        </select></label><p role="status" aria-atomic="true">{loadingApplications[job.id]?'Đang tải hồ sơ…':applicationPage?`${applicationPage.total} hồ sơ · ${applicationPage.items.length?((applicationPage.page-1)*applicationsPageSize+1):0}–${(applicationPage.page-1)*applicationsPageSize+applicationPage.items.length}`:''}</p></div>
        {applicationErrors[job.id]&&<div role="alert"><p>{applicationErrors[job.id]}</p><button type="button" className="secondary" onClick={()=>void refreshJobApplications(job.id)}>Tải lại hồ sơ</button></div>}
        {applicationPagination(job.id,'trên')}
        {!loadingApplications[job.id]&&applicationPage&&!applicationPage.items.length&&<p>{applicationQueries[job.id]?.status?'Không có hồ sơ khớp trạng thái đã chọn.':'Chưa có hồ sơ cho tin này.'}</p>}
        {!!applicationPage?.items.length&&<ul className="application-list">{applicationPage.items.map(application => <li key={application.id}>
        <div><strong>{statusLabel(application.status)}</strong><small>Nộp lúc {new Date(application.submittedAt).toLocaleString('vi-VN')} · CV phiên bản {application.resumeVersionNo}</small><SubmittedCoverLetter value={application.coverLetter}/>
          <div className="row application-actions">{(recruiterTransitions[application.status] ?? []).map(status => <button type="button" className="secondary" key={status} disabled={mutatingApplicationId === application.id} onClick={() => void changeApplicationStatus(job.id, application, status)}>{status === 'reviewing' ? 'Đang xem xét' : status === 'shortlisted' ? 'Chọn tiếp' : 'Loại khỏi quy trình'}</button>)}
            <button type="button" className="secondary" disabled={mutatingApplicationId === application.id} onClick={() => void toggleCandidateBlock(job.id, application)}>{application.candidateBlocked ? 'Bỏ chặn ứng viên' : 'Chặn ứng viên cho tin này'}</button>
            <button type="button" className="secondary" onClick={() => void toggleNotes(application)}>{expandedNotes[application.id] ? 'Ẩn ghi chú nội bộ' : 'Ghi chú nội bộ'}</button>
            <a href={`/api/applications/${application.id}/resume`}>Mở CV đã nộp</a>
            <a href={`/?profile=${application.candidateId}#shared-profile`} target="_blank" rel="noopener noreferrer">Xem hồ sơ được chia sẻ</a>
          </div>
          {expandedNotes[application.id] && <section className="application-notes"><strong>Ghi chú nội bộ — chỉ recruiter phụ trách tin đọc được</strong>
            {!notesByApplication[application.id]?.length ? <p>Chưa có ghi chú.</p> : <ul className="internal-note-list">{notesByApplication[application.id].map(note => <li key={note.id}><p>{note.body}</p><small>{new Date(note.createdAt).toLocaleString('vi-VN')}</small></li>)}</ul>}
            <label>Thêm ghi chú<textarea maxLength={4000} value={noteDrafts[application.id] ?? ''} onChange={event => setNoteDrafts(current => ({ ...current, [application.id]: event.target.value }))}/></label>
            <button type="button" className="secondary" disabled={savingNoteId === application.id || !noteDrafts[application.id]?.trim()} onClick={() => void addApplicationNote(application)}>{savingNoteId === application.id ? 'Đang lưu…' : 'Lưu ghi chú nội bộ'}</button>
          </section>}
        </div>
      </li>)}</ul>}{applicationPagination(job.id,'dưới')}</div>}
      </div>
    </article>;})}</div>
    {jobPagination('dưới')}
    </div>
    <ConfirmDialog
      open={!!archiveTarget}
      title="Ẩn tin tuyển dụng"
      message={archiveTarget ? `Ẩn tin "${archiveTarget.version.title}" khỏi danh sách công khai? Các hồ sơ ứng tuyển đã nộp vẫn được giữ nguyên.` : ''}
      confirmLabel="Ẩn tin"
      danger
      busy={busy}
      onClose={() => setArchiveTarget(null)}
      onConfirm={async () => { if (archiveTarget) await changeState(archiveTarget, 'archive'); }}
    />
  </section>;
}
