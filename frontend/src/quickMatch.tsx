import { useEffect, useState } from 'react';
import { api } from './api';
import { careerContextLabels, PairDetails, type Pair } from './pairMatching';
import { ValidatedForm } from './ui';

type Resume = { id: string; title: string; versions: { id: string; versionNo: number }[] };
type PublicJobOption = { id: string; version: { id: string; title: string }; company: { name: string } };

export function QuickMatchPanel({ resumes }: { resumes: Resume[] }) {
  const [open, setOpen] = useState(false);
  const [cvSource, setCvSource] = useState<'existing' | 'paste'>('existing');
  const [resumeVersionId, setResumeVersionId] = useState('');
  const [cvText, setCvText] = useState('');

  const [jdSource, setJdSource] = useState<'platform' | 'paste' | 'url'>('paste');
  const [platformJobs, setPlatformJobs] = useState<PublicJobOption[]>([]);
  const [jobVersionId, setJobVersionId] = useState('');
  const [jdUrl, setJdUrl] = useState('');
  const [jdTitle, setJdTitle] = useState('');
  const [jdText, setJdText] = useState('');
  const [careerContext, setCareerContext] = useState('');

  const [fetchingUrl, setFetchingUrl] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [pairId, setPairId] = useState<string>();
  const [pair, setPair] = useState<Pair>();

  const versions = resumes.flatMap(r => r.versions.map(v => ({ ...v, title: r.title })));

  useEffect(() => {
    if (!resumeVersionId && versions.length > 0) setResumeVersionId(versions[0].id);
  }, [resumeVersionId, versions]);

  useEffect(() => {
    if (!open || jdSource !== 'platform' || platformJobs.length > 0) return;
    let disposed = false;
    void api<{ items: PublicJobOption[] }>('/api/jobs?page=1&pageSize=20')
      .then(res => {
        if (!disposed) {
          setPlatformJobs(res.items);
          if (!jobVersionId && res.items.length > 0) setJobVersionId(res.items[0].version.id);
        }
      })
      .catch(() => { /* Keep empty state on failure */ });
    return () => { disposed = true; };
  }, [open, jdSource, platformJobs.length, jobVersionId]);

  useEffect(() => {
    if (!pairId) return;
    let disposed = false;
    let timer: ReturnType<typeof setTimeout>;
    async function poll() {
      try {
        const value = await api<Pair>(`/api/matching/pairs/${pairId}`);
        if (disposed) return;
        setPair(value);
        if (value.status === 'queued' || value.status === 'running') timer = setTimeout(() => void poll(), 2500);
      } catch (e) {
        if (!disposed) setError((e as Error).message);
      }
    }
    void poll();
    return () => { disposed = true; clearTimeout(timer); };
  }, [pairId]);

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
    setBusy(true); setError(''); setNotice(''); setPair(undefined); setPairId(undefined);
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
          {jdSource === 'platform' && <label>Tin tuyển dụng trên sàn<select value={jobVersionId} onChange={e => setJobVersionId(e.target.value)}>{platformJobs.map(j => <option key={j.version.id} value={j.version.id}>{j.version.title} — {j.company.name}</option>)}</select></label>}
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
      {error && <p className="error" role="alert">{error}</p>}
      <button disabled={busy || fetchingUrl || (cvSource === 'existing' && versions.length === 0) || (jdSource === 'platform' && !jobVersionId)}>
        {busy ? 'Đang gửi phân tích…' : 'Phân tích đối chiếu nhanh'}
      </button>
    </ValidatedForm>}
    {pair && <PairDetails pair={pair}/>}
  </section>;
}
