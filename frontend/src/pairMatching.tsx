import { useEffect, useState } from 'react';
import { api } from './api';

type Fact = { key: string; value: string; sourceType: string };
type Criterion = { requirementKey: string; requirement: string; importance: string; status: string; evidence: Fact[]; explanation: string; needsReview: boolean; requirementSourceType: string };
type Pair = { id: string; status: string; errorCode: string | null; provider: string; score: number | null; criteria: Criterion[] | null; location: string | null; workMode: string; salaryMin: number | null; salaryMax: number | null; salaryCurrency: string | null; salaryPeriod: string | null };
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

function PairDetails({ pair }: { pair: Pair }) {
  if (pair.status !== 'succeeded') return <p role="status">{pair.status === 'failed' || pair.status === 'cancelled' ? reason(pair.errorCode) : 'Đang chờ hoặc đang phân tích…'}</p>;
  const required = pair.criteria?.filter(c => c.importance === 'required' && c.status !== 'met' && c.status !== 'not_assessed').length ?? 0;
  const manual = pair.criteria?.filter(c => c.status === 'not_assessed').length ?? 0;
  return <div className="pair-result">
    <p><strong>Điểm đối chiếu {pair.score?.toFixed(2)}/100</strong> · rubric thử nghiệm{pair.provider === 'Fake' ? ' · Kết quả mô phỏng kiểm thử' : ''}</p>
    <p className="hint">Điểm cho cặp CV–JD này, không phải xác suất trúng tuyển hoặc kết luận năng lực. {required} yêu cầu bắt buộc chưa được chứng minh đầy đủ.</p>
    <p className="hint">Địa điểm: {pair.location ?? 'chưa nêu'} · hình thức: {pair.workMode} · lương: {pair.salaryMin == null && pair.salaryMax == null ? 'chưa công bố' : `${pair.salaryMin ?? '?'}–${pair.salaryMax ?? '?'} ${pair.salaryCurrency ?? ''} ${pair.salaryPeriod ?? ''}`}. Chưa đánh giá sự phù hợp các điều kiện này vì chưa có mong muốn được xác nhận của ứng viên.{manual > 0 && ` ${manual} tiêu chí cần đối chiếu thủ công, không tính vào điểm.`}</p>
    <details><summary>Xem tiêu chí và bằng chứng</summary><ul>{pair.criteria?.map(c => <li key={c.requirementKey}>
      <strong>{c.requirement}</strong> · {c.importance === 'required' ? 'Bắt buộc' : c.importance === 'optional' ? 'Ưu tiên' : 'Chưa phân loại'} · {c.status === 'not_assessed' ? 'Chưa đánh giá điều kiện' : c.status === 'met' ? 'Có bằng chứng đối chiếu' : c.status === 'partial' ? 'Có thông tin một phần' : 'Chưa có bằng chứng'}
      <p>{c.explanation}{c.needsReview ? ' Yêu cầu này cần người dùng đánh giá thêm.' : ''}</p>
      {c.requirementSourceType === 'user_statement' && <small>Yêu cầu do recruiter bổ sung.</small>}
      {c.evidence.map(f => <blockquote key={f.key}>{f.value}<small>{f.sourceType === 'user_statement' ? 'Thông tin ứng viên tự khai đã xác nhận' : 'Nội dung CV đã xác nhận'}</small></blockquote>)}
    </li>)}</ul></details>
  </div>;
}

export function PairAnalysisButton({ resumeVersionId, jobVersionId, applicationId, context = 'self' }: { resumeVersionId: string; jobVersionId: string; applicationId?: string; context?: string }) {
  const [id, setId] = useState<string>(); const [pair, setPair] = useState<Pair>();
  const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (!id) return;
    let disposed = false; let timer: ReturnType<typeof setTimeout>;
    async function poll() {
      try {
        const value = await api<Pair>(`/api/matching/pairs/${id}`);
        if (disposed) return;
        setPair(value);
        if (value.status === 'queued' || value.status === 'running') timer = setTimeout(() => void poll(), 2500);
      } catch (e) { if (!disposed) { setPair(undefined); setError((e as Error).message); } }
    }
    void poll(); return () => { disposed = true; clearTimeout(timer); };
  }, [id]);
  async function start() {
    setBusy(true); setError(''); setPair(undefined); setId(undefined);
    try {
      const value = await api<{ id: string }>('/api/matching/pairs', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resumeVersionId, jobVersionId, applicationId, context, idempotencyKey: crypto.randomUUID() }) });
      setId(value.id);
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  return <div><button className="secondary" disabled={busy || pair?.status === 'queued' || pair?.status === 'running'} onClick={() => void start()}>{busy ? 'Đang gửi…' : applicationId ? 'Phân tích theo hồ sơ lúc nộp' : 'Phân tích cặp CV–JD'}</button>
    {error && <p role="alert">{error}</p>}{pair && <PairDetails pair={pair}/>}</div>;
}

export function PairRecommendations({ direction, resumeVersionId, jobId }: { direction: 'jobs' | 'public_resumes' | 'applications'; resumeVersionId?: string; jobId?: string }) {
  const [batch, setBatch] = useState<Batch>(); const [results, setResults] = useState<Record<string, Pair>>({});
  const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  useEffect(() => {
    if (!batch) return;
    let disposed = false; let timer: ReturnType<typeof setTimeout>;
    async function poll() {
      let pending = false; const next: Record<string, Pair> = {};
      await Promise.all(batch!.items.filter(i => i.pairJobId).map(async i => {
        try { const value = await api<Pair>(`/api/matching/pairs/${i.pairJobId}`); next[value.id] = value; pending ||= value.status === 'queued' || value.status === 'running'; }
        catch { /* Revoked results are removed from the next render. */ }
      }));
      if (!disposed) { setResults(next); if (pending) timer = setTimeout(() => void poll(), 2500); }
    }
    void poll(); return () => { disposed = true; clearTimeout(timer); };
  }, [batch]);
  async function start(page = 1) {
    setBusy(true); setError(''); setBatch(undefined); setResults({});
    try { setBatch(await api<Batch>('/api/matching/recommendations', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ direction, resumeVersionId, jobId, topK: 3, page, idempotencyKey: crypto.randomUUID() }) })); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }
  const ordered = [...(batch?.items ?? [])].sort((a, b) => {
    const x = a.pairJobId ? results[a.pairJobId]?.score : null; const y = b.pairJobId ? results[b.pairJobId]?.score : null;
    return x != null && y != null ? y - x : x != null ? -1 : y != null ? 1 : 0;
  });
  return <section className="pair-recommendations" aria-label="Đối chiếu AI có bằng chứng"><h4>Phân tích AI có bằng chứng</h4><button disabled={busy || direction === 'jobs' && !resumeVersionId} onClick={() => void start()}>{busy ? 'Đang chọn tập…' : direction === 'applications' ? 'Phân tích tối đa 3 hồ sơ trong trang' : 'Gợi ý và phân tích tối đa 3 cặp'}</button>
    <p className="hint">Rubric thử nghiệm: bắt buộc trọng số 2, ưu tiên/chưa phân loại trọng số 1; đáp ứng 1, một phần 0,5, chưa có bằng chứng 0. Chỉ các mục đã phân tích thành công được xếp theo điểm. AI không thay đổi trạng thái đơn.</p>
    {error && <p role="alert">{error}</p>}{batch && <><p>Đã chọn {batch.selectedCount}/{batch.searchSpaceCount} mục để phân tích sâu{direction === 'applications' ? ` · trang ${batch.page}, 20 hồ sơ/trang, chọn theo thứ tự nộp` : ' sau bước tìm từ khóa'}.</p>
      <ol>{ordered.map(i => <li key={`${i.resumeVersionId}:${i.jobVersionId}:${i.applicationId}`}><strong>{i.label}</strong>{i.retrievalScore != null && <small> · điểm tìm từ khóa {i.retrievalScore.toFixed(3)}</small>}
        {i.applicationId && <a href={`/api/applications/${i.applicationId}/resume`} target="_blank" rel="noreferrer">Mở CV đã nộp</a>}
        {i.pairJobId ? results[i.pairJobId] ? <PairDetails pair={results[i.pairJobId]}/> : <p>Đang tải hoặc kết quả không còn quyền truy cập.</p> : <p>Chưa chấm · {reason(i.errorCode)}</p>}
        {!i.pairJobId && i.applicationId && <PairAnalysisButton resumeVersionId={i.resumeVersionId} jobVersionId={i.jobVersionId} applicationId={i.applicationId} context="application"/>}
      </li>)}</ol>{direction === 'applications' && <div className="row">{batch.page > 1 && <button disabled={busy} onClick={() => void start(batch.page - 1)}>Trang trước</button>}{batch.hasMore && <button disabled={busy} onClick={() => void start(batch.page + 1)}>Trang tiếp</button>}</div>}</>}
  </section>;
}
