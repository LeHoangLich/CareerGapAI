import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { api } from './api';
import { CandidateKeywordRecommendations, RecruiterKeywordResumeSearch } from './matching';

type Session = { authenticated: boolean; userId?: string; roles: string[] };
type Company = { id: string; name: string; slug: string; description: string | null; websiteUrl: string | null; location: string | null; industry: string | null; sizeLabel: string | null; rowVersion: number };
type JobVersion = { id: string; versionNo: number; title: string; description: string; requirementsText: string; benefitsText: string | null; industry: string | null; location: string | null; employmentType: string; workMode: string; experienceMin: number | null; experienceMax: number | null; salaryMin: number | null; salaryMax: number | null; salaryCurrency: string | null; salaryPeriod: string | null; language: string; contentHash: string };
type JobRequirement = { minimumYears?: number | null; relation?: string; alternatives?: string[]; needsReview?: boolean; requirementId: string; text: string; importance: string; sourceType: string };
type JobRequirementEvidence = { requirementId: string; sourceType: string; startOffset: number | null; endOffset: number | null; quote: string | null; statement: string | null };
type JobDataRevision = { id: string; jobVersionId: string; revisionNo: number; parentRevisionId: string | null; origin: string; structuredData: { schemaVersion: number; requirements: JobRequirement[] }; sourceText: { requirementsText: string }; evidence: JobRequirementEvidence[]; dataHash: string; parserVersion: string; confirmedBy: string | null; confirmedAt: string | null; createdAt: string };
type Job = { id: string; status: string; companyId: string; company: Company; version: JobVersion; applicationDeadline: string | null; acceptingApplications: boolean; rowVersion: number; publishedAt: string | null; closedAt: string | null };
type Page<T> = { items: T[]; page: number; pageSize: number; total: number };
type Saved = { savedAt: string; job: Job };
type Resume = { id: string; title: string; versions: { id: string; versionNo: number; createdAt: string }[] };
type Application = { id: string; jobId: string; jobTitle: string; companyName: string; status: string; submittedAt: string; statusChangedAt: string; resumeVersionNo: number; rowVersion: number; candidateId: string; candidateBlocked: boolean };
type ApplicationHistory = { fromStatus: string | null; toStatus: string; changedAt: string };
type ApplicationNote = { id: string; body: string; createdAt: string };
type Notification = { id: string; notificationType: string; applicationId: string | null; title: string; body: string; createdAt: string; readAt: string | null; deduplicationKey: string };
type SearchFilters = { q: string; skill: string; industry: string; location: string; workMode: string; employmentType: string; experienceYears: string };
type JobForm = { title: string; description: string; requirementsText: string; benefitsText: string; industry: string; location: string; employmentType: string; workMode: string; experienceMin: string; experienceMax: string; language: string };

const emptyFilters: SearchFilters = { q: '', skill: '', industry: '', location: '', workMode: '', employmentType: '', experienceYears: '' };
const emptyJob: JobForm = { title: '', description: '', requirementsText: '', benefitsText: '', industry: '', location: '', employmentType: 'full_time', workMode: 'unspecified', experienceMin: '', experienceMax: '', language: 'vi' };
const activeApplicationStatuses = ['submitted', 'reviewing', 'shortlisted'];
const statusLabels: Record<string, string> = { submitted: 'Đã nộp', reviewing: 'Đang xem xét', shortlisted: 'Được chọn tiếp', rejected: 'Không tiếp tục', withdrawn: 'Đã rút' };
const recruiterTransitions: Record<string, string[]> = { submitted: ['reviewing', 'shortlisted', 'rejected'], reviewing: ['shortlisted', 'rejected'], shortlisted: ['reviewing', 'rejected'] };

function statusLabel(status: string) { return statusLabels[status] ?? status; }
const importanceLabels: Record<string, string> = { required: 'Bắt buộc', optional: 'Tùy chọn', unspecified: 'Chưa xác định' };

function toQuery(filters: SearchFilters, page: number) {
  const params = new URLSearchParams({ page: String(page), pageSize: '10' });
  for (const [key, value] of Object.entries(filters)) if (value.trim()) params.set(key, value.trim());
  return params.toString();
}

function JobCard({ job, candidate, saved, onToggleSaved, resumes, application, selectedVersionId, onSelectVersion, onApply, applying }: {
  job: Job; candidate: boolean; saved: boolean; onToggleSaved: (job: Job) => void;
  resumes: Resume[]; application?: Application; selectedVersionId?: string;
  onSelectVersion: (jobId: string, versionId: string) => void; onApply: (job: Job) => void; applying: boolean;
}) {
  const version = job.version;
  const resumeVersions = resumes.flatMap(resume => resume.versions.map(item => ({ ...item, resumeTitle: resume.title })));
  return <article className="job-card" data-job-id={job.id}>
    <div className="job-title-row"><div><h3>{version.title}</h3><p>{job.company.name} · {version.location || job.company.location || 'Chưa có địa điểm'}</p></div><span className={`job-status ${job.status}`}>{job.status === 'published' ? job.acceptingApplications ? 'Đang nhận hồ sơ' : 'Đã hết hạn' : 'Đã đóng'}</span></div>
    <div className="job-meta"><span>{version.employmentType}</span><span>{version.workMode}</span><span>{version.industry || 'Chưa phân ngành'}</span><span>{version.experienceMin ?? '—'}–{version.experienceMax ?? '—'} năm</span></div>
    <p className="job-description">{version.description}</p>
    <details><summary>Yêu cầu và quyền lợi</summary><p><strong>Yêu cầu</strong><br/>{version.requirementsText}</p>{version.benefitsText && <p><strong>Quyền lợi</strong><br/>{version.benefitsText}</p>}</details>
    {job.applicationDeadline && <small>Hạn nhận hồ sơ: {new Date(job.applicationDeadline).toLocaleString('vi-VN')}</small>}
    {candidate && <button className="secondary save-job" aria-pressed={saved} onClick={() => onToggleSaved(job)}>{saved ? 'Bỏ lưu việc' : 'Lưu việc'}</button>}
    {candidate && application && <p className="hint" role="status">Bạn đã nộp hồ sơ · {application.status} · CV phiên bản {application.resumeVersionNo}</p>}
    {candidate && !application && job.acceptingApplications && <div className="apply-row">
      {resumeVersions.length ? <><label>CV gửi<select aria-label={`CV gửi cho ${version.title}`} value={selectedVersionId ?? resumeVersions[0].id} onChange={e => onSelectVersion(job.id, e.target.value)}>
        {resumeVersions.map(item => <option key={item.id} value={item.id}>{item.resumeTitle} · v{item.versionNo}</option>)}
      </select></label><button disabled={applying} onClick={() => onApply(job)}>{applying ? 'Đang nộp…' : 'Nộp hồ sơ'}</button></>
        : <p className="hint">Tải CV lên mục “CV riêng của bạn” để nộp hồ sơ.</p>}
    </div>}
  </article>;
}

export function PublicJobBrowser({ session }: { session?: Session }) {
  const [filters, setFilters] = useState(emptyFilters);
  const [appliedFilters, setAppliedFilters] = useState(emptyFilters);
  const [page, setPage] = useState(1);
  const [results, setResults] = useState<Page<Job>>();
  const [saved, setSaved] = useState<Saved[]>([]);
  const [resumes, setResumes] = useState<Resume[]>([]);
  const [applications, setApplications] = useState<Application[]>([]);
  const [applicationHistory, setApplicationHistory] = useState<Record<string, ApplicationHistory[]>>({});
  const [expandedHistory, setExpandedHistory] = useState<Record<string, boolean>>({});
  const [withdrawingId, setWithdrawingId] = useState<string>();
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [selectedVersionByJob, setSelectedVersionByJob] = useState<Record<string, string>>({});
  const [applyingJobId, setApplyingJobId] = useState<string>();
  const [applicationMessage, setApplicationMessage] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const submissionKeys = useRef(new Map<string, string>());
  const candidate = session?.authenticated === true && session.roles.includes('Candidate');

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
    if (!session?.authenticated) { setResumes([]); setApplications([]); setNotifications([]); return; }
    try {
      const noticeRequest = api<Notification[]>('/api/notifications');
      if (candidate) {
        const [resumeList, applicationPage, noticeList] = await Promise.all([
          api<Resume[]>('/api/resumes?page=1'), api<Page<Application>>('/api/applications/mine?page=1&pageSize=50'), noticeRequest
        ]);
        setResumes(resumeList); setApplications(applicationPage.items); setNotifications(noticeList);
        setSelectedVersionByJob(current => {
          const valid = new Set(resumeList.flatMap(resume => resume.versions.map(version => version.id)));
          const next = Object.fromEntries(Object.entries(current).filter(([, versionId]) => valid.has(versionId)));
          return next;
        });
      } else {
        setResumes([]); setApplications([]); setNotifications(await noticeRequest);
      }
    } catch (e) { setError((e as Error).message); }
  }, [candidate, session?.authenticated]);
  useEffect(() => {
    const refresh = () => { void refreshApplicationData(); };
    refresh();
    window.addEventListener('resume-list-updated', refresh);
    return () => window.removeEventListener('resume-list-updated', refresh);
  }, [refreshApplicationData]);
  const savedIds = useMemo(() => new Set(saved.map(x => x.job.id)), [saved]);
  const activeApplications = useMemo(() => new Map(applications.filter(x => activeApplicationStatuses.includes(x.status)).map(x => [x.jobId, x])), [applications]);
  async function toggleSaved(job: Job) {
    setError('');
    try {
      const isSaved = savedIds.has(job.id);
      await api(`/api/saved-jobs/${job.id}`, { method: isSaved ? 'DELETE' : 'PUT' });
      await refreshSaved();
    } catch (e) { setError((e as Error).message); }
  }
  async function apply(job: Job) {
    const versions = resumes.flatMap(resume => resume.versions);
    const resumeVersionId = selectedVersionByJob[job.id] ?? versions[0]?.id;
    if (!resumeVersionId) return;
    const keyRef = `${job.id}:${resumeVersionId}`;
    const key = submissionKeys.current.get(keyRef) ?? crypto.randomUUID();
    submissionKeys.current.set(keyRef, key);
    setApplyingJobId(job.id); setError(''); setApplicationMessage('');
    try {
      const application = await api<Application>(`/api/jobs/${job.id}/applications`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resumeVersionId, submissionKey: key }) });
      setApplicationMessage(`Đã nộp hồ sơ cho “${application.jobTitle}”.`);
      await refreshApplicationData();
    } catch (e) { setError((e as Error).message); }
    finally { setApplyingJobId(undefined); }
  }
  async function withdraw(application: Application) {
    setWithdrawingId(application.id); setError(''); setApplicationMessage('');
    try {
      await api(`/api/applications/${application.id}/withdraw`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion: application.rowVersion }) });
      setApplicationMessage(`Bạn đã rút hồ sơ cho “${application.jobTitle}”.`);
      await refreshApplicationData();
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
  async function markNotificationRead(notification: Notification) {
    try {
      await api(`/api/notifications/${notification.id}/read`, { method: 'PUT' });
      setNotifications(current => current.map(item => item.id === notification.id ? { ...item, readAt: new Date().toISOString() } : item));
    } catch (e) { setError((e as Error).message); }
  }
  const items = results?.items ?? [];
  return <section className="card job-browser">
    <div className="section-heading"><div><p className="eyebrow">TIN TUYỂN DỤNG</p><h2>Tìm công việc</h2></div><span>{results?.total ?? 0} kết quả</span></div>
    <form className="job-filters" onSubmit={e => { e.preventDefault(); setPage(1); setAppliedFilters({ ...filters }); }}>
      <label>Từ khóa<input value={filters.q} onChange={e => setFilters({ ...filters, q: e.target.value })} maxLength={120} placeholder="Tên vị trí, công ty hoặc mô tả"/></label>
      <label>Kỹ năng<input value={filters.skill} onChange={e => setFilters({ ...filters, skill: e.target.value })} maxLength={100} placeholder="Ví dụ: PostgreSQL"/></label>
      <label>Ngành<input value={filters.industry} onChange={e => setFilters({ ...filters, industry: e.target.value })} maxLength={100}/></label>
      <label>Địa điểm<input value={filters.location} onChange={e => setFilters({ ...filters, location: e.target.value })} maxLength={250}/></label>
      <label>Hình thức<select value={filters.workMode} onChange={e => setFilters({ ...filters, workMode: e.target.value })}><option value="">Tất cả</option><option value="onsite">Tại văn phòng</option><option value="hybrid">Linh hoạt</option><option value="remote">Từ xa</option><option value="unspecified">Chưa xác định</option></select></label>
      <label>Loại công việc<select value={filters.employmentType} onChange={e => setFilters({ ...filters, employmentType: e.target.value })}><option value="">Tất cả</option><option value="full_time">Toàn thời gian</option><option value="part_time">Bán thời gian</option><option value="contract">Hợp đồng</option><option value="internship">Thực tập</option><option value="temporary">Tạm thời</option></select></label>
      <label>Kinh nghiệm của bạn (năm)<input type="number" min="0" max="99.99" step="0.01" value={filters.experienceYears} onChange={e => setFilters({ ...filters, experienceYears: e.target.value })}/></label>
      <button disabled={busy}>Tìm việc</button>
    </form>
    {candidate && <CandidateKeywordRecommendations resumes={resumes} onSearchTitle={title => {
      const next = { ...filters, q: title };
      setFilters(next); setAppliedFilters(next); setPage(1);
    }}/>}
    {error && <p role="alert" className="error">{error}</p>}
    {busy && <p role="status">Đang tìm tin…</p>}
    {!busy && !items.length && <p>Chưa có tin công khai phù hợp.</p>}
    <div className="job-list">{items.map(job => <JobCard key={job.id} job={job} candidate={candidate} saved={savedIds.has(job.id)} onToggleSaved={jobToSave => void toggleSaved(jobToSave)} resumes={resumes} application={activeApplications.get(job.id)} selectedVersionId={selectedVersionByJob[job.id]} onSelectVersion={(jobId, versionId) => setSelectedVersionByJob(current => ({ ...current, [jobId]: versionId }))} onApply={jobToApply => void apply(jobToApply)} applying={applyingJobId === job.id}/>)}</div>
    {results && results.total > results.pageSize && <div className="row job-pages"><button className="secondary" disabled={page <= 1 || busy} onClick={() => setPage(x => x - 1)}>Trước</button><span>Trang {page} / {Math.max(1, Math.ceil(results.total / results.pageSize))}</span><button className="secondary" disabled={page * results.pageSize >= results.total || busy} onClick={() => setPage(x => x + 1)}>Sau</button></div>}
    {candidate && saved.length > 0 && <details className="saved-jobs"><summary>Việc đã lưu ({saved.length})</summary><div className="job-list">{saved.map(x => <JobCard key={x.job.id} job={x.job} candidate={true} saved={true} onToggleSaved={jobToSave => void toggleSaved(jobToSave)} resumes={resumes} application={activeApplications.get(x.job.id)} selectedVersionId={selectedVersionByJob[x.job.id]} onSelectVersion={(jobId, versionId) => setSelectedVersionByJob(current => ({ ...current, [jobId]: versionId }))} onApply={jobToApply => void apply(jobToApply)} applying={applyingJobId === x.job.id}/>)}</div></details>}
    {candidate && <section className="application-center"><div className="section-heading"><div><p className="eyebrow">HỒ SƠ ĐÃ NỘP</p><h3>Đơn ứng tuyển của bạn</h3></div><span>{applications.length}</span></div>
      {applicationMessage && <p role="status">{applicationMessage}</p>}
      {!applications.length ? <p>Chưa có đơn ứng tuyển.</p> : <ul className="application-list">{applications.map(application => <li key={application.id}><div><strong>{application.jobTitle}</strong><small>{application.companyName} · {new Date(application.submittedAt).toLocaleString('vi-VN')} · {statusLabel(application.status)}</small><div className="row application-actions"><a href={`/api/applications/${application.id}/resume`}>CV đã nộp · v{application.resumeVersionNo}</a>{activeApplicationStatuses.includes(application.status) && <button className="secondary" disabled={withdrawingId === application.id} onClick={() => void withdraw(application)}>{withdrawingId === application.id ? 'Đang rút…' : 'Rút hồ sơ'}</button>}<button className="secondary" onClick={() => void toggleHistory(application)}>{expandedHistory[application.id] ? 'Ẩn lịch sử' : 'Lịch sử trạng thái'}</button></div>{expandedHistory[application.id] && <ul className="status-history">{(applicationHistory[application.id] ?? []).map((entry, index) => <li key={`${entry.changedAt}-${index}`}><span>{entry.fromStatus ? `${statusLabel(entry.fromStatus)} → ` : ''}{statusLabel(entry.toStatus)}</span><small>{new Date(entry.changedAt).toLocaleString('vi-VN')}</small></li>)}</ul>}</div></li>)}</ul>}
    </section>}
    {session?.authenticated && <section className="application-center notification-center"><div className="section-heading"><div><p className="eyebrow">CẬP NHẬT</p><h3>Thông báo</h3></div><span>{notifications.filter(x => !x.readAt).length} chưa đọc</span></div>
      {!notifications.length ? <p>Chưa có thông báo.</p> : <ul className="application-list">{notifications.map(notification => <li key={notification.id} className={notification.readAt ? 'notification-read' : ''}><div><strong>{notification.title}</strong><small>{notification.body} · {new Date(notification.createdAt).toLocaleString('vi-VN')}</small></div>{!notification.readAt && <button className="secondary" onClick={() => void markNotificationRead(notification)}>Đánh dấu đã đọc</button>}</li>)}</ul>}
    </section>}
    {!candidate && <p className="hint">Đăng nhập bằng tài khoản ứng viên để lưu tin quan tâm.</p>}
  </section>;
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
    {!latest && <button className="secondary" disabled={busy} onClick={() => void extract()}>{busy ? 'Đang tách dòng…' : 'Tách yêu cầu từ JD (local)'}</button>}
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
          </select></label><button className="secondary" disabled={!manualText.trim()} onClick={addManualRequirement}>Thêm yêu cầu</button></div>
        {!!manualRequirements.length && <ul className="job-manual-list">{manualRequirements.map((item, index) => <li key={`${item.text}-${index}`}><span>{item.text} · {importanceLabels[item.importance]}</span><button className="link-button" onClick={() => setManualRequirements(current => current.filter((_, i) => i !== index))}>Bỏ</button></li>)}</ul>}
        <button disabled={busy} onClick={() => void confirm()}>{busy ? 'Đang lưu…' : 'Xác nhận yêu cầu JD'}</button>
      </>}
    </>}
  </section>;
}

export function RecruiterJobManager() {
  const [companies, setCompanies] = useState<Company[]>([]);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [applicationsByJob, setApplicationsByJob] = useState<Record<string, Page<Application>>>({});
  const [expandedApplications, setExpandedApplications] = useState<Record<string, boolean>>({});
  const [expandedNotes, setExpandedNotes] = useState<Record<string, boolean>>({});
  const [notesByApplication, setNotesByApplication] = useState<Record<string, ApplicationNote[]>>({});
  const [noteDrafts, setNoteDrafts] = useState<Record<string, string>>({});
  const [mutatingApplicationId, setMutatingApplicationId] = useState<string>();
  const [savingNoteId, setSavingNoteId] = useState<string>();
  const [loadingApplications, setLoadingApplications] = useState<string>();
  const [companyForm, setCompanyForm] = useState({ name: '', description: '', websiteUrl: '', location: '', industry: '', sizeLabel: '' });
  const [editingCompany, setEditingCompany] = useState<Company>();
  const [companyId, setCompanyId] = useState('');
  const [jobForm, setJobForm] = useState<JobForm>(emptyJob);
  const [deadline, setDeadline] = useState('');
  const [editingJob, setEditingJob] = useState<Job>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  const refresh = useCallback(async () => {
    const [companyList, jobPage] = await Promise.all([api<Company[]>('/api/companies/mine'), api<Page<Job>>('/api/jobs/mine?page=1&pageSize=50')]);
    setCompanies(companyList); setJobs(jobPage.items);
    setCompanyId(current => companyList.some(x => x.id === current) ? current : (companyList[0]?.id ?? ''));
  }, []);

  useEffect(() => { void refresh().catch(e => setError((e as Error).message)); }, [refresh]);

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
    const content = { ...jobForm, experienceMin: jobForm.experienceMin === '' ? null : Number(jobForm.experienceMin), experienceMax: jobForm.experienceMax === '' ? null : Number(jobForm.experienceMax), salaryMin: null, salaryMax: null, salaryCurrency: null, salaryPeriod: null };
    const applicationDeadline = deadline ? new Date(deadline).toISOString() : null;
    try {
      if (editingJob) await api<Job>(`/api/jobs/${editingJob.id}`, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion: editingJob.rowVersion, applicationDeadline, content }) });
      else await api<Job>('/api/jobs', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ companyId, applicationDeadline, content }) });
      setEditingJob(undefined); setJobForm(emptyJob); setDeadline(''); await refresh(); setMessage('JD đã được lưu thành phiên bản mới.');
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  function editJob(job: Job) {
    setEditingJob(job); setCompanyId(job.companyId);
    setJobForm({ title: job.version.title, description: job.version.description, requirementsText: job.version.requirementsText, benefitsText: job.version.benefitsText ?? '', industry: job.version.industry ?? '', location: job.version.location ?? '', employmentType: job.version.employmentType, workMode: job.version.workMode, experienceMin: job.version.experienceMin?.toString() ?? '', experienceMax: job.version.experienceMax?.toString() ?? '', language: job.version.language });
    if (job.applicationDeadline) {
      const date = new Date(job.applicationDeadline);
      setDeadline(new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16));
    } else setDeadline('');
  }

  async function changeState(job: Job, action: 'publish' | 'close') {
    setBusy(true); setError(''); setMessage('');
    try { await api<Job>(`/api/jobs/${job.id}/${action}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ rowVersion: job.rowVersion }) }); await refresh(); setMessage(action === 'publish' ? 'Tin đã được công khai.' : 'Tin đã được đóng.'); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  async function toggleApplications(job: Job) {
    if (expandedApplications[job.id]) { setExpandedApplications(current => ({ ...current, [job.id]: false })); return; }
    setExpandedApplications(current => ({ ...current, [job.id]: true }));
    if (applicationsByJob[job.id]) return;
    setLoadingApplications(job.id); setError('');
    try {
      await refreshJobApplications(job.id);
    } catch (e) { setError((e as Error).message); setExpandedApplications(current => ({ ...current, [job.id]: false })); }
    finally { setLoadingApplications(undefined); }
  }

  async function refreshJobApplications(jobId: string) {
    const page = await api<Page<Application>>(`/api/jobs/${jobId}/applications?page=1&pageSize=50`);
    setApplicationsByJob(current => ({ ...current, [jobId]: page }));
  }

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
        const notes = await api<ApplicationNote[]>(`/api/applications/${application.id}/notes`);
        setNotesByApplication(current => ({ ...current, [application.id]: notes }));
      } catch (e) { setError((e as Error).message); }
    }
  }

  async function addApplicationNote(application: Application) {
    const body = noteDrafts[application.id]?.trim();
    if (!body) return;
    setSavingNoteId(application.id); setError(''); setMessage('');
    try {
      await api<ApplicationNote>(`/api/applications/${application.id}/notes`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ body }) });
      const notes = await api<ApplicationNote[]>(`/api/applications/${application.id}/notes`);
      setNotesByApplication(current => ({ ...current, [application.id]: notes }));
      setNoteDrafts(current => ({ ...current, [application.id]: '' })); setMessage('Đã thêm ghi chú nội bộ.');
    } catch (e) { setError((e as Error).message); }
    finally { setSavingNoteId(undefined); }
  }

  return <section className="card recruiter-jobs">
    <div className="section-heading"><div><p className="eyebrow">NHÀ TUYỂN DỤNG</p><h2>Công ty và tin tuyển dụng</h2></div></div>
    {error && <p role="alert" className="error">{error}</p>}{message && <p role="status">{message}</p>}
    <div className="company-row"><strong>Công ty của tôi</strong>{companies.map(company => <div className="company-chip" key={company.id}><span>{company.name}</span><button className="link-button" onClick={() => { setEditingCompany(company); setCompanyForm({ name: company.name, description: company.description ?? '', websiteUrl: company.websiteUrl ?? '', location: company.location ?? '', industry: company.industry ?? '', sizeLabel: company.sizeLabel ?? '' }); }}>Sửa</button></div>)}</div>
    <form className="editor-form" onSubmit={saveCompany}><h3>{editingCompany ? 'Cập nhật công ty' : 'Tạo hồ sơ công ty'}</h3>
      <label>Tên công ty<input required maxLength={200} value={companyForm.name} onChange={e => setCompanyForm({ ...companyForm, name: e.target.value })}/></label>
      <label>Giới thiệu<textarea maxLength={4000} value={companyForm.description} onChange={e => setCompanyForm({ ...companyForm, description: e.target.value })}/></label>
      <div className="form-columns"><label>Website HTTPS<input type="url" placeholder="https://example.com" value={companyForm.websiteUrl} onChange={e => setCompanyForm({ ...companyForm, websiteUrl: e.target.value })}/></label><label>Địa điểm<input maxLength={250} value={companyForm.location} onChange={e => setCompanyForm({ ...companyForm, location: e.target.value })}/></label><label>Ngành<input maxLength={100} value={companyForm.industry} onChange={e => setCompanyForm({ ...companyForm, industry: e.target.value })}/></label><label>Quy mô<input maxLength={50} placeholder="11-50" value={companyForm.sizeLabel} onChange={e => setCompanyForm({ ...companyForm, sizeLabel: e.target.value })}/></label></div>
      <div className="row"><button disabled={busy}>{busy ? 'Đang lưu…' : editingCompany ? 'Lưu công ty' : 'Tạo công ty'}</button>{editingCompany && <button type="button" className="secondary" onClick={() => { setEditingCompany(undefined); setCompanyForm({ name: '', description: '', websiteUrl: '', location: '', industry: '', sizeLabel: '' }); }}>Hủy sửa</button>}</div>
    </form>
    <form className="editor-form" onSubmit={saveJob}><h3>{editingJob ? `Sửa JD · bản mới từ v${editingJob.version.versionNo}` : 'Tạo tin nháp'}</h3>
      {!editingJob && <label>Công ty<select required value={companyId} onChange={e => setCompanyId(e.target.value)}><option value="">Chọn công ty</option>{companies.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>}
      <label>Chức danh<input required maxLength={200} value={jobForm.title} onChange={e => setJobForm({ ...jobForm, title: e.target.value })}/></label>
      <label>Mô tả công việc<textarea required maxLength={12000} value={jobForm.description} onChange={e => setJobForm({ ...jobForm, description: e.target.value })}/></label>
      <label>Yêu cầu và kỹ năng<textarea required maxLength={12000} value={jobForm.requirementsText} onChange={e => setJobForm({ ...jobForm, requirementsText: e.target.value })}/></label>
      <label>Quyền lợi<textarea maxLength={5000} value={jobForm.benefitsText} onChange={e => setJobForm({ ...jobForm, benefitsText: e.target.value })}/></label>
      <div className="form-columns"><label>Ngành<input maxLength={100} value={jobForm.industry} onChange={e => setJobForm({ ...jobForm, industry: e.target.value })}/></label><label>Địa điểm<input maxLength={250} value={jobForm.location} onChange={e => setJobForm({ ...jobForm, location: e.target.value })}/></label><label>Loại công việc<select value={jobForm.employmentType} onChange={e => setJobForm({ ...jobForm, employmentType: e.target.value })}><option value="full_time">Toàn thời gian</option><option value="part_time">Bán thời gian</option><option value="contract">Hợp đồng</option><option value="internship">Thực tập</option><option value="temporary">Tạm thời</option></select></label><label>Hình thức<select value={jobForm.workMode} onChange={e => setJobForm({ ...jobForm, workMode: e.target.value })}><option value="unspecified">Chưa xác định</option><option value="onsite">Tại văn phòng</option><option value="hybrid">Linh hoạt</option><option value="remote">Từ xa</option></select></label><label>Kinh nghiệm tối thiểu (năm)<input type="number" min="0" max="99.99" step="0.01" value={jobForm.experienceMin} onChange={e => setJobForm({ ...jobForm, experienceMin: e.target.value })}/></label><label>Kinh nghiệm tối đa (năm)<input type="number" min="0" max="99.99" step="0.01" value={jobForm.experienceMax} onChange={e => setJobForm({ ...jobForm, experienceMax: e.target.value })}/></label><label>Ngôn ngữ JD<select value={jobForm.language} onChange={e => setJobForm({ ...jobForm, language: e.target.value })}><option value="vi">Tiếng Việt</option><option value="en">English</option><option value="vi-en">Việt–Anh</option></select></label><label>Hạn nhận hồ sơ<input type="datetime-local" value={deadline} onChange={e => setDeadline(e.target.value)}/></label></div>
      <div className="row"><button disabled={busy || (!editingJob && !companyId)}>{busy ? 'Đang lưu…' : editingJob ? 'Lưu phiên bản mới' : 'Tạo tin nháp'}</button>{editingJob && <button type="button" className="secondary" onClick={() => { setEditingJob(undefined); setJobForm(emptyJob); setDeadline(''); }}>Hủy sửa</button>}</div>
    </form>
    <h3>Tin của tôi</h3>
    {!jobs.length && <p>Chưa có tin. Hãy tạo công ty rồi bắt đầu bằng một tin nháp.</p>}
    <div className="job-list">{jobs.map(job => <article className="job-card" key={job.id}><div className="job-title-row"><div><h3>{job.version.title}</h3><p>{job.company.name} · phiên bản {job.version.versionNo}</p></div><span className={`job-status ${job.status}`}>{job.status}</span></div><p>{job.version.description}</p><div className="row"><button className="secondary" disabled={busy || job.status === 'closed'} onClick={() => editJob(job)}>Sửa JD</button>{job.status === 'draft' && <button disabled={busy} onClick={() => void changeState(job, 'publish')}>Công khai</button>}{job.status === 'published' && <button className="secondary" disabled={busy} onClick={() => void changeState(job, 'close')}>Đóng tin</button>}<button className="secondary" disabled={loadingApplications === job.id} onClick={() => void toggleApplications(job)}>{loadingApplications === job.id ? 'Đang tải…' : expandedApplications[job.id] ? 'Ẩn hồ sơ' : `Hồ sơ ứng tuyển (${applicationsByJob[job.id]?.total ?? 'xem'})`}</button></div>
      <JobRequirementReview key={`${job.id}:${job.version.id}`} job={job}/>
      <RecruiterKeywordResumeSearch jobId={job.id} enabled={job.status === 'published' && job.acceptingApplications}/>
      {expandedApplications[job.id] && <div className="job-applications"><h4>Hồ sơ ứng tuyển</h4>{!applicationsByJob[job.id]?.items.length ? <p>Chưa có hồ sơ cho tin này.</p> : <ul className="application-list">{applicationsByJob[job.id].items.map(application => <li key={application.id}>
        <div><strong>{statusLabel(application.status)}</strong><small>Nộp lúc {new Date(application.submittedAt).toLocaleString('vi-VN')} · CV phiên bản {application.resumeVersionNo}</small>
          <div className="row application-actions">{(recruiterTransitions[application.status] ?? []).map(status => <button className="secondary" key={status} disabled={mutatingApplicationId === application.id} onClick={() => void changeApplicationStatus(job.id, application, status)}>{status === 'reviewing' ? 'Đang xem xét' : status === 'shortlisted' ? 'Chọn tiếp' : 'Loại khỏi quy trình'}</button>)}
            <button className="secondary" disabled={mutatingApplicationId === application.id} onClick={() => void toggleCandidateBlock(job.id, application)}>{application.candidateBlocked ? 'Bỏ chặn ứng viên' : 'Chặn ứng viên cho tin này'}</button>
            <button className="secondary" onClick={() => void toggleNotes(application)}>{expandedNotes[application.id] ? 'Ẩn ghi chú nội bộ' : 'Ghi chú nội bộ'}</button>
            <a href={`/api/applications/${application.id}/resume`}>Mở CV đã nộp</a>
          </div>
          {expandedNotes[application.id] && <section className="application-notes"><strong>Ghi chú nội bộ — chỉ recruiter phụ trách tin đọc được</strong>
            {!notesByApplication[application.id]?.length ? <p>Chưa có ghi chú.</p> : <ul className="internal-note-list">{notesByApplication[application.id].map(note => <li key={note.id}><p>{note.body}</p><small>{new Date(note.createdAt).toLocaleString('vi-VN')}</small></li>)}</ul>}
            <label>Thêm ghi chú<textarea maxLength={4000} value={noteDrafts[application.id] ?? ''} onChange={event => setNoteDrafts(current => ({ ...current, [application.id]: event.target.value }))}/></label>
            <button className="secondary" disabled={savingNoteId === application.id || !noteDrafts[application.id]?.trim()} onClick={() => void addApplicationNote(application)}>{savingNoteId === application.id ? 'Đang lưu…' : 'Lưu ghi chú nội bộ'}</button>
          </section>}
        </div>
      </li>)}</ul>}</div>}
    </article>)}</div>
  </section>;
}
