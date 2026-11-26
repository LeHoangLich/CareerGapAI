import { useEffect, useState } from 'react';
import { api } from './api';
import { careerContextLabels, PairDetails, type Pair } from './pairMatching';
import { useViewActive } from './persistentView';
import { ValidatedForm } from './ui';
import { useAiJobPolling } from './useAiJobPolling';

type Resume = { id: string; title: string; versions: { id: string; versionNo: number }[] };
type PublicJobOption = { id: string; version: { id: string; title: string }; company: { name: string } };

export function QuickMatchPanel({ resumes, preselectedJob, defaultOpen = false }: { resumes: Resume[]; preselectedJob?: PublicJobOption; defaultOpen?: boolean }) {
  const active = useViewActive();
  const [open, setOpen] = useState(defaultOpen || !!preselectedJob);
  const [cvSource, setCvSource] = useState<'existing' | 'paste'>('existing');
  const [resumeVersionId, setResumeVersionId] = useState('');
  const [cvText, setCvText] = useState('');

  const [jdSource, setJdSource] = useState<'platform' | 'paste' | 'url'>('paste');
  const [platformQuery, setPlatformQuery] = useState('');
  const [platformJobs, setPlatformJobs] = useState<PublicJobOption[]>([]);
  const [platformLoading, setPlatformLoading] = useState(false);
  const [platformError, setPlatformError] = useState('');
  const [platformRetry, setPlatformRetry] = useState(0);
  const [jobVersionId, setJobVersionId] = useState(preselectedJob?.version.id ?? '');
  const [jdUrl, setJdUrl] = useState('');
  const [jdTitle, setJdTitle] = useState('');
  const [jdText, setJdText] = useState('');
  const [careerContext, setCareerContext] = useState('');

  const [fetchingUrl, setFetchingUrl] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [pairId, setPairId] = useState<string>();
  const [matchedJobId, setMatchedJobId] = useState<string>();
  const [pair, setPair] = useState<Pair>();

  const versions = resumes.flatMap(r => r.versions.map(v => ({ ...v, title: r.title })));

  useEffect(() => {
    if (!resumeVersionId && versions.length > 0) setResumeVersionId(versions[0].id);
  }, [resumeVersionId, versions]);

  useEffect(() => {
    if (!preselectedJob) return;
    setJdSource('platform');
    setJobVersionId(preselectedJob.version.id);
  }, [preselectedJob]);

  useEffect(() => {
    if (!open || jdSource !== 'platform') return;
    const controller = new AbortController();
    setPlatformLoading(true);
    setPlatformError('');
    const timer = setTimeout(() => {
      const params = new URLSearchParams({ page: '1', pageSize: '20' });
      if (platformQuery.trim()) params.set('q', platformQuery.trim());
      void api<{ items: PublicJobOption[] }>(`/api/jobs?${params}`, { signal: controller.signal })
        .then(res => {
          if (controller.signal.aborted) return;
          setPlatformJobs(res.items);
          setJobVersionId(current => {
            if (preselectedJob?.version.id === current) return current;
            if (res.items.some(item => item.version.id === current)) return current;
            return res.items[0]?.version.id ?? preselectedJob?.version.id ?? '';
          });
        })
        .catch(e => {
          if (!controller.signal.aborted) setPlatformError((e as Error).message);
        })
        .finally(() => {
          if (!controller.signal.aborted) setPlatformLoading(false);
        });
    }, platformQuery.trim() ? 250 : 0);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [open, jdSource, platformQuery, platformRetry, preselectedJob]);

  const { timedOut, retryPolling } = useAiJobPolling({
    pollKey: pairId,
    active,
    maxAttempts: 60,
    intervalMs: 2500,
    tick: async signal => {
      const value = await api<Pair>(`/api/matching/pairs/${pairId}`, { signal });
      if (signal.aborted) return false;
      setPair(value);
      return value.status !== 'queued' && value.status !== 'running';
    },
    onError: e => setError((e as Error).message),
  });

  const jobOptions = preselectedJob && !platformJobs.some(j => j.version.id === preselectedJob.version.id)
    ? [preselectedJob, ...platformJobs]
    : platformJobs;

  async function previewUrl() {
    if (!jdUrl.trim()) { setError('Nhập URL JD trước khi tải.'); return; }
    setFetchingUrl(true); setError(''); setNotice('');
    try {
      const res = await api<{ finalUrl: string; title: string; text: string; truncated: boolean }>('/api/matching/jd-url-preview', {
        method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ url: jdUrl.trim() })
      });
      setJdTitle(res.title);
      setJdText(res.text);
      setNotice(`Đã tải nội dung từ ${res.finalUrl}${res.truncated ? ' (đã rút gọn về 12.000 ký tự)' : ''}. Hãy kiểm tra và chỉnh sửa trước khi phân tích.`);
    } catch (e) { setError((e as Error).message); } finally { setFetchingUrl(false); }
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const selectedJob = jdSource === 'platform' ? jobOptions.find(j => j.version.id === jobVersionId) : undefined;
    setBusy(true); setError(''); setNotice(''); setPair(undefined); setPairId(undefined);
    setMatchedJobId(selectedJob?.id);
    try {
      const res = await api<{ id: string }>('/api/matching/quick-pairs', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          cvSource,
          resumeVersionId: cvSource === 'existing' ? resumeVersionId : null,
          cvText: cvSource === 'paste' ? cvText : null,
          jdSource,
          jobVersionId: jdSource === 'platform' ? jobVersionId : null,
          jdTitle: jdSource !== 'platform' ? jdTitle : null,
          jdText: jdSource !== 'platform' ? jdText : null,
          jdUrl: jdSource === 'url' ? jdUrl : null,
          careerContext: careerContext || null,
          idempotencyKey: crypto.randomUUID()
        })
      });
      setPairId(res.id);
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  return <section className="card quick-match-panel" aria-label="Đối chiếu nhanh nhiều nguồn CV/JD">
    <div className="row">
      <div><p className="eyebrow">PHÂN TÍCH ĐA NGUỒN</p><h3>Đối chiếu nhanh CV &amp; JD</h3></div>
      <button type="button" className="secondary" aria-expanded={open} onClick={() => setOpen(v => !v)}>
        {open ? 'Thu gọn form đối chiếu nhanh' : 'Mở form đối chiếu nhanh'}
      </button>
    </div>
    {open && <ValidatedForm onSubmit={e => void submit(e)}>
      <div className="form-columns">
        <fieldset><legend>1. Nguồn CV</legend>
          <label>Chọn nguồn CV<select value={cvSource} onChange={e => setCvSource(e.target.value as 'existing' | 'paste')}>
            <option value="existing">CV đã xác nhận trong hệ thống</option>
            <option value="paste">Dán văn bản CV nhanh</option>
          </select></label>
          {cvSource === 'existing'
            ? (versions.length > 0
              ? <label>Phiên bản CV<select value={resumeVersionId} onChange={e => setResumeVersionId(e.target.value)}>{versions.map(v => <option key={v.id} value={v.id}>{v.title} · v{v.versionNo}</option>)}</select></label>
              : <p className="hint">Chưa có CV nào. Hãy tải CV ở mục Quản lý CV riêng hoặc chọn Dán văn bản CV.</p>)
            : <label>Nội dung CV<textarea required minLength={20} maxLength={20000} rows={6} value={cvText} onChange={e => setCvText(e.target.value)} placeholder="Dán tóm tắt kỹ năng, kinh nghiệm từ CV của bạn (tối thiểu 20 ký tự)…"/></label>}
        </fieldset>

        <fieldset><legend>2. Nguồn JD</legend>
          <label>Chọn nguồn JD<select value={jdSource} onChange={e => setJdSource(e.target.value as 'platform' | 'paste' | 'url')}>
            <option value="paste">Dán văn bản JD</option>
            <option value="url">Nhập từ URL ngoài hệ thống</option>
            <option value="platform">Việc làm đang đăng trên sàn</option>
          </select></label>
          {jdSource === 'platform' && <>
            <label>Tìm tin trên sàn<input value={platformQuery} maxLength={120} onChange={e => setPlatformQuery(e.target.value)} placeholder="Nhập tên vị trí hoặc công ty…"/></label>
            {platformLoading && <p className="hint" role="status">Đang tải danh sách việc làm…</p>}
            {platformError && <p className="error" role="alert">{platformError} <button type="button" className="secondary" onClick={() => setPlatformRetry(v => v + 1)}>Thử lại</button></p>}
            <label>Tin tuyển dụng trên sàn<select value={jobVersionId} onChange={e => setJobVersionId(e.target.value)}>
              {!jobOptions.length && <option value="">Không có tin tuyển dụng phù hợp</option>}
              {jobOptions.map(j => <option key={j.version.id} value={j.version.id}>{j.version.title} — {j.company.name}</option>)}
            </select></label>
          </>}
          {jdSource === 'url' && <div className="row"><label>URL tin tuyển dụng<input type="url" placeholder="https://example.com/jobs/backend" value={jdUrl} onChange={e => setJdUrl(e.target.value)}/></label><button type="button" className="secondary" disabled={fetchingUrl || busy} onClick={() => void previewUrl()}>{fetchingUrl ? 'Đang tải…' : 'Tải nội dung JD'}</button></div>}
          {jdSource !== 'platform' && <>
            <label>Tiêu đề vị trí<input maxLength={200} value={jdTitle} onChange={e => setJdTitle(e.target.value)} placeholder="Ví dụ: Backend .NET Developer"/></label>
            <label>Nội dung yêu cầu JD<textarea required minLength={10} maxLength={12000} rows={6} value={jdText} onChange={e => setJdText(e.target.value)} placeholder="Dán hoặc chỉnh sửa các yêu cầu tuyển dụng tại đây…"/></label>
          </>}
        </fieldset>
      </div>

      <label>3. Bối cảnh nghề nghiệp<select value={careerContext} onChange={e => setCareerContext(e.target.value)}>
        <option value="">Mặc định</option>
        {Object.entries(careerContextLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
      </select></label>

      {notice && <p className="hint" role="status">{notice}</p>}
      {error && <p className="error" role="alert">{error}{timedOut && <> <button type="button" className="secondary" onClick={retryPolling}>Kiểm tra lại kết quả</button></>}</p>}
      <button type="submit" disabled={busy || fetchingUrl || (cvSource === 'existing' && versions.length === 0) || (jdSource === 'platform' && !jobVersionId)}>
        {busy ? 'Đang gửi phân tích…' : 'Phân tích đối chiếu nhanh'}
      </button>
    </ValidatedForm>}
    {pair && <PairDetails pair={pair} jobId={matchedJobId}/>}
  </section>;
}
