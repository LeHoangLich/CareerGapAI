import { WeeklyRoadmap } from './weeklyRoadmap';
import { useState } from 'react';
import { api } from './api';
import { jobHref, jobIdFromHash } from './jobLinks';
import { LearningResources } from './learning';
import { useViewActive } from './persistentView';
import { ResultPlacement } from './resultPlacement';
import { useAiJobPolling } from './useAiJobPolling';
import { AiBadge, StaticLoading, StatusBadge } from './ui';

type Fact = { key: string; value: string; sourceType: string };
type Criterion = { requirementKey: string; requirement: string; importance: string; status: string; evidence: Fact[]; explanation: string; needsReview: boolean; requirementSourceType: string };
export type CriterionGroupSummary = { key: string; label: string; status: string; totalCriteria: number; metCount: number; partialCount: number; noEvidenceCount: number; summary: string };
export type Pair = { id: string; context: string; status: string; errorCode: string | null; provider: string; score: number | null; criteria: Criterion[] | null; location: string | null; workMode: string; salaryMin: number | null; salaryMax: number | null; salaryCurrency: string | null; salaryPeriod: string | null; careerContext?: string | null; criterionGroups?: CriterionGroupSummary[] | null };
export const careerContextLabels: Record<string, string> = { career_start: 'Bắt đầu nghề (Career Start)', career_switch: 'Chuyển ngành (Career Switch)', promotion: 'Thăng tiến (Promotion)' };
const groupStatusLabels: Record<string, string> = { met: 'Đáp ứng', partial: 'Một phần', no_evidence: 'Chưa có bằng chứng', not_requested: 'Không yêu cầu' };
type Item = { resumeVersionId: string; jobVersionId: string; applicationId: string | null; label: string; retrievalScore: number | null; pairJobId: string | null; errorCode: string | null };
type Batch = { searchSpaceCount: number; selectedCount: number; page: number; hasMore: boolean; items: Item[] };
const reasons: Record<string, string> = {
  confirmed_resume_required: 'CV chưa có dữ liệu xác nhận phù hợp với thời điểm phân tích.',
  confirmed_job_required: 'JD chưa có yêu cầu được xác nhận phù hợp với thời điểm phân tích.',
  no_eligible_cv_facts: 'CV công khai chưa có dữ liệu xác nhận từ tài liệu được phép dùng.',
  quota_exhausted: 'Đã hết ngân sách AI chung.', user_quota_exhausted: 'Đã hết hạn mức AI của tài khoản.',
  user_concurrency_limit: 'Đã đủ số phân tích đang chạy. Hãy đợi rồi thử lại.',
  match_output_budget_required: 'Chưa cấu hình hạn mức đầu ra cho phân tích cặp.',
  no_assessable_requirements: 'JD chưa có tiêu chí đủ đầu vào để chấm điểm.',
  matching_input_limit: 'Đầu vào vượt giới hạn phân tích thử nghiệm.',
  match_output_invalid: 'Phản hồi AI không đạt kiểm tra bằng chứng; chưa tạo điểm.',
  access_revoked: 'Quyền truy cập đã thay đổi.', provider_timeout: 'AI hết thời gian phản hồi; chưa tạo điểm.',
  ai_output_truncated: 'Phản hồi AI chưa đầy đủ; chưa tạo điểm.', ai_disabled: 'AI đang tạm dừng.',
};
const reason = (code: string | null) => code ? reasons[code] ?? 'Chưa phân tích được. Hãy kiểm tra đầu vào hoặc thử lại sau.' : 'Chưa được chọn phân tích sâu trong lượt này.';

export function PairDetails({ pair, jobId }: { pair: Pair; jobId?: string }) {
  if (pair.status !== 'succeeded') {
    if (pair.status === 'failed' || pair.status === 'cancelled') {
      return <p role="status">{reason(pair.errorCode)}</p>;
    }
    return <StaticLoading variant="analysis" message="AI đang đối chiếu từng tiêu chí CV × JD…" />;
  }
  const metCount = pair.criteria?.filter(c => c.status === 'met').length ?? 0;
  const partialCount = pair.criteria?.filter(c => c.status === 'partial').length ?? 0;
  const required = pair.criteria?.filter(c => c.importance === 'required' && c.status !== 'met' && c.status !== 'not_assessed').length ?? 0;
  const gapCount = pair.criteria?.filter(c => c.status === 'no_evidence').length ?? 0;
  const totalCount = pair.criteria?.length ?? 0;
  const manual = pair.criteria?.filter(c => c.status === 'not_assessed').length ?? 0;
  const currentJobId = jobIdFromHash(window.location.hash);
  const targetJobId = jobId && jobId !== currentJobId ? jobId : undefined;
  return <div className="pair-result">
    <AiBadge>{pair.provider === 'Fake' ? 'AI mô phỏng · cần đối chiếu' : 'AI đối chiếu · cần rà soát'}</AiBadge>
    <div className="kpi-strip" aria-label="Tổng hợp kết quả đối chiếu">
      <div className="kpi-card">
        <span className="kpi-label">Điểm đối chiếu</span>
        <strong className="kpi-value">{pair.score?.toFixed(2) ?? '0.00'}/100</strong>
        <span className="kpi-sub">{pair.careerContext ? careerContextLabels[pair.careerContext] ?? pair.careerContext : 'Thang chấm tiêu chí'}</span>
      </div>
      <div className="kpi-card">
        <span className="kpi-label">Yêu cầu khớp</span>
        <strong className="kpi-value is-matched">{metCount} / {totalCount}</strong>
        <span className="kpi-sub">Có bằng chứng nguyên văn</span>
      </div>
      <div className="kpi-card">
        <span className="kpi-label">Khớp một phần</span>
        <strong className="kpi-value is-partial">{partialCount}</strong>
        <span className="kpi-sub">Cần bổ sung chi tiết</span>
      </div>
      <div className="kpi-card">
        <span className="kpi-label">Chưa có bằng chứng</span>
        <strong className="kpi-value is-gap">{gapCount}</strong>
        <span className="kpi-sub">{required} yêu cầu bắt buộc</span>
      </div>
    </div>
    <p className="hint">Điểm cho cặp CV–JD này, không phải xác suất trúng tuyển hoặc kết luận năng lực.{pair.provider === 'Fake' ? ' Kết quả mô phỏng kiểm thử.' : ''}</p>
    <p className="hint">Địa điểm: {pair.location ?? 'chưa nêu'} · hình thức: {pair.workMode} · lương: {pair.salaryMin == null && pair.salaryMax == null ? 'chưa công bố' : `${pair.salaryMin ?? '?'}–${pair.salaryMax ?? '?'} ${pair.salaryCurrency ?? ''} ${pair.salaryPeriod ?? ''}`}. Chưa đánh giá sự phù hợp các điều kiện này vì chưa có mong muốn được xác nhận của ứng viên.{manual > 0 && ` ${manual} tiêu chí cần đối chiếu thủ công, không tính vào điểm.`}</p>
    {!!pair.criterionGroups?.length && <section className="criterion-groups" aria-label="Tổng hợp 6 nhóm tiêu chí"><h4>Tổng hợp 6 nhóm tiêu chí</h4><div className="form-columns">{pair.criterionGroups.map(g => <article className="card" key={g.key}><header className="row"><strong>{g.label}</strong><StatusBadge state={g.status === 'met' ? 'matched' : g.status === 'partial' ? 'partial' : g.status === 'no_evidence' ? 'gap' : 'neutral'}>{groupStatusLabels[g.status] ?? g.status}</StatusBadge></header><p className="hint">{g.summary}</p></article>)}</div></section>}
    {pair.context === 'self' && <WeeklyRoadmap key={pair.id} pairId={pair.id} jobId={targetJobId}/>}
    <details><summary>Xem tiêu chí và bằng chứng</summary><ul className="grid gap-3 pl-0">{pair.criteria?.map(c => <li className="ai-evidence-card" key={c.requirementKey}>
      <div className="row">
        <strong>{c.requirement}</strong>
        <StatusBadge state={c.status === 'met' ? 'matched' : c.status === 'partial' ? 'partial' : c.status === 'no_evidence' ? 'gap' : 'neutral'}>
          {c.status === 'not_assessed' ? 'Chưa đánh giá điều kiện' : c.status === 'met' ? 'Có bằng chứng đối chiếu' : c.status === 'partial' ? 'Có thông tin một phần' : 'Chưa có bằng chứng'}
        </StatusBadge>
      </div>
      <small className="hint">{c.importance === 'required' ? 'Bắt buộc' : c.importance === 'optional' ? 'Ưu tiên' : 'Chưa phân loại'}</small>
      <p className="m-0">{c.explanation}{c.needsReview ? ' Yêu cầu này cần người dùng đánh giá thêm.' : ''}</p>
      {c.requirementSourceType === 'user_statement' && <small>Yêu cầu do recruiter bổ sung.</small>}
      {c.evidence.map(f => <blockquote className="ai-evidence-quote" key={f.key}>{f.value}<small>{f.sourceType === 'user_statement' ? 'Thông tin ứng viên tự khai đã xác nhận' : 'Nội dung CV đã xác nhận'}</small></blockquote>)}
      {pair.context === 'self' && ['partial', 'no_evidence'].includes(c.status) && <LearningResources pairId={pair.id} requirementKey={c.requirementKey}/>}
    </li>)}</ul></details>
    {pair.context === 'self' && <nav className="row pair-next-steps" aria-label="Bước tiếp theo sau đối chiếu"><a className="secondary" href="#cv-builder">Mở trình tạo CV để bổ sung</a><a className="secondary" href="#private-resumes">Quản lý CV của tôi</a>{targetJobId && <a className="secondary" href={jobHref(targetJobId)}>Xem tin tuyển dụng &amp; Ứng tuyển</a>}</nav>}
  </div>;
}

type PairSelection = { resumeVersionId: string; jobVersionId: string; applicationId?: string; context?: string; jobId?: string };
export function PairAnalysisButton(props: PairSelection) {
  return <PairAnalysis key={`${props.resumeVersionId}:${props.jobVersionId}:${props.applicationId ?? ''}:${props.context ?? 'self'}`} {...props}/>;
}

function PairAnalysis({ resumeVersionId, jobVersionId, applicationId, context = 'self', jobId }: PairSelection) {
  const active = useViewActive();
  const [pollId, setPollId] = useState<string>();
  const [pair, setPair] = useState<Pair>();
  const [careerContext, setCareerContext] = useState('');
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState('');
  const { timedOut, retryPolling } = useAiJobPolling({
    pollKey: pollId,
    active,
    maxAttempts: 60,
    intervalMs: 2500,
    tick: async signal => {
      const value = await api<Pair>(`/api/matching/pairs/${pollId}`, { signal });
      if (signal.aborted) return false;
      setPair(value);
      return value.status !== 'queued' && value.status !== 'running';
    },
    onError: e => { setError((e as Error).message); },
  });
  async function start() {
    setBusy(true); setError(''); setNotice(''); setPair(undefined); setPollId(undefined);
    try {
      const value = await api<{ id: string }>('/api/matching/pairs', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resumeVersionId, jobVersionId, applicationId, context, careerContext: careerContext || null, idempotencyKey: crypto.randomUUID() }) });
      setPollId(value.id);
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  async function readLatest() {
    setBusy(true); setError(''); setNotice(''); setPair(undefined); setPollId(undefined);
    const query = new URLSearchParams({ resumeVersionId, jobVersionId, context });
    if (applicationId) query.set('applicationId', applicationId);
    try {
      const value = await api<{ pair: Pair | null }>(`/api/matching/pairs/latest?${query}`);
      if (value.pair) {
        setPair(value.pair);
        if (value.pair.status === 'queued' || value.pair.status === 'running') setPollId(value.pair.id);
        setNotice('Kết quả đã lưu theo dữ liệu và phương pháp tại lần phân tích trước. Xem lại không tạo yêu cầu AI mới.');
      } else setNotice('Chưa có kết quả đã lưu có thể xem cho cặp CV–JD này.');
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  return <div>{context === 'self' && <label>Bối cảnh nghề nghiệp<select value={careerContext} disabled={busy} onChange={e => setCareerContext(e.target.value)}><option value="">Mặc định</option>{Object.entries(careerContextLabels).map(([k, v]) => <option key={k} value={k}>{v}</option>)}</select></label>}
    <div className="row"><button type="button" className="secondary" disabled={busy || pair?.status === 'queued' || pair?.status === 'running'} onClick={() => void start()}>{busy ? 'Đang gửi…' : applicationId ? 'Phân tích theo hồ sơ lúc nộp' : 'Phân tích cặp CV–JD'}</button>
    <button type="button" className="secondary" disabled={busy} onClick={() => void readLatest()}>Xem kết quả đã lưu</button></div>
    {notice && <p className="hint" role="status">{notice}</p>}
    {error && <p role="alert">{error}{timedOut && <> <button type="button" className="secondary" onClick={() => { setError(''); retryPolling(); }}>Kiểm tra lại kết quả</button></>}</p>}{pair && <PairDetails pair={pair} jobId={jobId}/>}</div>;
}

export function PairRecommendations({ direction, resumeVersionId, jobId, resultsTarget }: { direction: 'jobs' | 'public_resumes' | 'applications'; resumeVersionId?: string; jobId?: string; resultsTarget?: HTMLElement | null }) {
  const active = useViewActive();
  const [batch, setBatch] = useState<Batch>();
  const [batchKey, setBatchKey] = useState<string>();
  const [results, setResults] = useState<Record<string, Pair>>({});
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const { timedOut, retryPolling } = useAiJobPolling({
    pollKey: batchKey,
    active,
    maxAttempts: 60,
    intervalMs: 2500,
    tick: async signal => {
      if (!batch) return true;
      let pending = false;
      const next: Record<string, Pair> = {};
      await Promise.all(batch.items.filter(i => i.pairJobId).map(async i => {
        try {
          const value = await api<Pair>(`/api/matching/pairs/${i.pairJobId}`, { signal });
          next[value.id] = value;
          pending ||= value.status === 'queued' || value.status === 'running';
        } catch (e) {
          if (e instanceof Error && e.name === 'AbortError') throw e;
          /* Revoked results are removed from the next render. */
        }
      }));
      if (signal.aborted) return false;
      setResults(next);
      return !pending;
    },
    onError: e => setError((e as Error).message),
  });
  async function start(page = 1) {
    setBusy(true); setError(''); setBatch(undefined); setBatchKey(undefined); setResults({});
    try {
      const nextBatch = await api<Batch>('/api/matching/recommendations', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ direction, resumeVersionId, jobId, topK: 3, page, idempotencyKey: crypto.randomUUID() }) });
      setBatch(nextBatch);
      if (nextBatch.items.some(i => i.pairJobId)) setBatchKey(`${direction}:${page}:${crypto.randomUUID()}`);
    }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  const ordered = [...(batch?.items ?? [])].sort((a, b) => {
    const x = a.pairJobId ? results[a.pairJobId]?.score : null; const y = b.pairJobId ? results[b.pairJobId]?.score : null;
    return x != null && y != null ? y - x : x != null ? -1 : y != null ? 1 : 0;
  });
  return <section className="pair-recommendations" aria-label="Đối chiếu AI có bằng chứng"><h4>Phân tích AI có bằng chứng</h4><button type="button" disabled={busy || direction === 'jobs' && !resumeVersionId} onClick={() => void start()}>{busy ? 'Đang chọn tập…' : direction === 'applications' ? 'Phân tích tối đa 3 hồ sơ trong trang' : 'Gợi ý và phân tích tối đa 3 cặp'}</button>
    {resultsTarget === undefined ? <p className="hint">Thang chấm tiêu chí: bắt buộc trọng số 2, ưu tiên/chưa phân loại trọng số 1; đáp ứng 1, một phần 0,5, chưa có bằng chứng 0. Chỉ các mục đã phân tích thành công được xếp theo điểm. AI không thay đổi trạng thái đơn.</p>
      : <details className="pair-method-details"><summary>Phương pháp và giới hạn</summary><p className="hint">Thang chấm tiêu chí: bắt buộc trọng số 2, ưu tiên/chưa phân loại trọng số 1; đáp ứng 1, một phần 0,5, chưa có bằng chứng 0. Chỉ các mục đã phân tích thành công được xếp theo điểm. AI không thay đổi trạng thái đơn.</p></details>}
    <ResultPlacement target={resultsTarget}>
    {(busy || error || batch) && <section className={resultsTarget !== undefined ? 'matching-output' : undefined} aria-label="Kết quả đối chiếu AI">
    {resultsTarget !== undefined && <div className="section-heading"><div><p className="eyebrow">ĐỐI CHIẾU CÓ BẰNG CHỨNG</p><h3>Kết quả phân tích AI</h3></div><span className="badge">CV–JD</span></div>}
    {busy && <StaticLoading label="Đang chọn tập phân tích…"/>}
    {error && <p role="alert">{error}{timedOut && <> <button type="button" className="secondary" onClick={() => { setError(''); retryPolling(); }}>Kiểm tra lại kết quả</button></>}</p>}{batch && <><p>Đã chọn {batch.selectedCount}/{batch.searchSpaceCount} mục để phân tích sâu{direction === 'applications' ? ` · trang ${batch.page}, 20 hồ sơ/trang, chọn theo thứ tự nộp` : ' sau bước tìm từ khóa'}.</p>
      <ol>{ordered.map(i => <li key={`${i.resumeVersionId}:${i.jobVersionId}:${i.applicationId}`}><strong>{i.label}</strong>{i.retrievalScore != null && <small> · điểm tìm từ khóa {i.retrievalScore.toFixed(3)}</small>}
        {i.applicationId && <a href={`/api/applications/${i.applicationId}/resume`} target="_blank" rel="noreferrer">Mở CV đã nộp</a>}
        {i.pairJobId ? results[i.pairJobId] ? <PairDetails pair={results[i.pairJobId]}/> : <p>Đang tải hoặc kết quả không còn quyền truy cập.</p> : <p>Chưa chấm · {reason(i.errorCode)}</p>}
        {!i.pairJobId && i.applicationId && <PairAnalysisButton resumeVersionId={i.resumeVersionId} jobVersionId={i.jobVersionId} applicationId={i.applicationId} context="application"/>}
      </li>)}</ol>{direction === 'applications' && <div className="row">{batch.page > 1 && <button type="button" disabled={busy} onClick={() => void start(batch.page - 1)}>Trang trước</button>}{batch.hasMore && <button type="button" disabled={busy} onClick={() => void start(batch.page + 1)}>Trang tiếp</button>}</div>}</>}
    </section>}
    </ResultPlacement>
  </section>;
}
