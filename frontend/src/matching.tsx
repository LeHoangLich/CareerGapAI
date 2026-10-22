import { useMemo, useState } from 'react';
import { api } from './api';
import { PairAnalysisButton, PairRecommendations } from './pairMatching';

type ResumeList = { id: string; title: string; versions: { id: string; versionNo: number; createdAt?: string }[] };
type KeywordOverlap = { count: number; queryTermCount: number; terms: string[] };
type ConfirmedRevisionMatch = { methodVersion: string; status: string; resumeRevisionNo: number | null; jobRevisionNo: number | null; eligibleSkillCount: number | null; requirementCount: number | null; requiredRequirementCount: number | null; matchedRequirementCount: number | null; matchedRequiredRequirementCount: number | null; optionalRequirementCount: number | null; unspecifiedRequirementCount: number | null };
type Run<T> = { methodVersion: string; configurationHash: string; scoreKind: string; searchSpaceCount: number; assessedCount: number; unreadableCount: number; zeroOverlapCount: number; noQueryTermsCount: number; items: T[] };
type JobRecommendation = { jobId: string; jobVersionId: string; title: string; companyName: string; location: string | null; workMode: string; employmentType: string; applicationDeadline: string | null; retrievalScore: number; keywordOverlap: KeywordOverlap; confirmedRevisionMatch: ConfirmedRevisionMatch };
type ResumeRecommendation = { candidateId: string; resumeVersionId: string; versionNo: number; displayName: string; headline: string | null; publishedAt: string; retrievalScore: number; keywordOverlap: KeywordOverlap; confirmedRevisionMatch: ConfirmedRevisionMatch };

function revisionDiagnosticText(value: ConfirmedRevisionMatch, audience: 'candidate' | 'recruiter') {
  if (value.status === 'ready') {
    return `Revision xác nhận · ${value.matchedRequirementCount}/${value.requirementCount} yêu cầu có term overlap · required ${value.matchedRequiredRequirementCount}/${value.requiredRequirementCount}. Đây là chẩn đoán riêng, không thay điểm hoặc thứ tự retrieval.`;
  }
  const statusText: Record<string, string> = {
    resume_and_job_revision_unconfirmed: 'CV và JD chưa có revision xác nhận; chỉ dùng baseline retrieval.',
    resume_revision_unconfirmed: 'CV chưa có revision xác nhận; chỉ dùng baseline retrieval.',
    job_revision_unconfirmed: 'JD chưa có revision xác nhận; chỉ dùng baseline retrieval.',
    no_public_document_skills: 'CV công khai chưa có kỹ năng xác nhận từ nội dung tài liệu; không dùng kỹ năng tự khai báo cho recruiter.',
    resume_revision_empty: 'Revision CV đã xác nhận không có kỹ năng để đối chiếu.',
    job_revision_empty: 'Revision JD đã xác nhận không có yêu cầu để đối chiếu.',
    resume_revision_data_unavailable: 'Revision CV không đủ dữ liệu/provenance hợp lệ để đối chiếu.',
    job_revision_data_unavailable: 'Revision JD không đủ dữ liệu/provenance hợp lệ để đối chiếu.'
  };
  return statusText[value.status] ?? `${audience === 'candidate' ? 'Chẩn đoán CV–JD' : 'Chẩn đoán revision'} chưa khả dụng (${value.status}); baseline retrieval vẫn độc lập.`;
}

export function CandidateKeywordRecommendations({ resumes, onSearchTitle }: { resumes: ResumeList[]; onSearchTitle: (title: string) => void }) {
  const versions = useMemo(() => resumes.flatMap(resume => resume.versions.map(version => ({ ...version, title: resume.title }))), [resumes]);
  const [resumeVersionId, setResumeVersionId] = useState('');
  const [run, setRun] = useState<Run<JobRecommendation>>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const selected = versions.find(x => x.id === resumeVersionId) ?? versions[0];
  async function recommend() {
    if (!selected) return;
    setResumeVersionId(selected.id); setBusy(true); setError(''); setRun(undefined);
    try { setRun(await api<Run<JobRecommendation>>(`/api/matching/jobs?resumeVersionId=${encodeURIComponent(selected.id)}`)); }
    catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <section className="matching-panel" aria-label="Gợi ý việc bằng từ khóa">
    <div className="section-heading"><div><p className="eyebrow">BASELINE L09</p><h3>Gợi ý việc theo CV</h3></div><span className="badge">Điểm từ khóa</span></div>
    <p>Dùng một CV riêng của bạn để xếp hạng JD đang nhận hồ sơ. Chỉ từ khóa trùng; đây là retrieval score 0–1, không phải điểm phù hợp AI hay kết luận năng lực.</p>
    <div className="row matching-controls"><label>CV dùng để tìm<select aria-label="CV dùng để gợi ý việc" value={selected?.id ?? ''} onChange={event => { setResumeVersionId(event.target.value); setRun(undefined); }} disabled={!versions.length}>
      {!versions.length && <option value="">Chưa có CV</option>}
      {versions.map(version => <option key={version.id} value={version.id}>{version.title} · v{version.versionNo}</option>)}
    </select></label><button disabled={busy || !selected} onClick={() => void recommend()}>{busy ? 'Đang xếp hạng…' : 'Tìm việc theo CV'}</button></div>
    {!versions.length && <p className="hint">Tải một CV riêng lên trước; không cần công khai CV để tìm việc cho chính bạn.</p>}
    <PairRecommendations key={selected?.id ?? 'empty'} direction="jobs" resumeVersionId={selected?.id}/>
    {error && <p role="alert" className="error">{error}</p>}
    {run && <>
      <p className="hint" role="status">Đã xét {run.assessedCount}/{run.searchSpaceCount} JD · {run.zeroOverlapCount} JD không trùng từ khóa · {run.noQueryTermsCount} JD chưa có từ khóa để tìm · phương pháp {run.methodVersion}.</p>
      {run.items.length ? <ol className="matching-results">{run.items.map(item => <li key={`${item.jobId}:${item.jobVersionId}`}>
        <div><strong>{item.title}</strong><small>{item.companyName} · {item.location ?? 'Chưa có địa điểm'} · {item.workMode}</small>
          <small>retrieval_score {item.retrievalScore.toFixed(3)} · trùng {item.keywordOverlap.count}/{item.keywordOverlap.queryTermCount} từ khóa JD</small>
          {item.keywordOverlap.terms.length > 0 && <small>Từ khóa trùng: {item.keywordOverlap.terms.join(', ')}</small>}
          <small>{revisionDiagnosticText(item.confirmedRevisionMatch, 'candidate')}</small>
        </div><button className="secondary" onClick={() => onSearchTitle(item.title)}>Tìm tin này trong danh sách</button>
        {selected && <PairAnalysisButton key={`${selected.id}:${item.jobVersionId}`} resumeVersionId={selected.id} jobVersionId={item.jobVersionId}/>}
      </li>)}</ol> : <p>Không tìm thấy từ khóa trùng trong các JD đang nhận hồ sơ.</p>}
    </>}
  </section>;
}

export function RecruiterKeywordResumeSearch({ jobId, enabled }: { jobId: string; enabled: boolean }) {
  const [run, setRun] = useState<Run<ResumeRecommendation>>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  async function search() {
    setBusy(true); setError(''); setRun(undefined);
    try { setRun(await api<Run<ResumeRecommendation>>(`/api/matching/public-resumes?jobId=${encodeURIComponent(jobId)}`)); }
    catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  if (!enabled) return null;
  return <section className="matching-panel recruiter-matching" aria-label="Tìm CV công khai theo JD">
    <div className="row"><strong>CV công khai theo JD</strong><button className="secondary" disabled={busy} onClick={() => void search()}>{busy ? 'Đang xếp hạng…' : 'Tìm ứng viên bằng từ khóa'}</button></div>
    <p className="hint">Chỉ xét CV đang được candidate công khai và file gốc sẵn sàng; CV riêng đã nộp nhưng không công khai không nằm trong kho này.</p>
    <PairRecommendations key={jobId} direction="public_resumes" jobId={jobId}/>
    {error && <p role="alert" className="error">{error}</p>}
    {run && <>
      <p className="hint" role="status">Đã đọc được {run.assessedCount}/{run.searchSpaceCount} CV công khai · {run.unreadableCount} CV không đọc được đã bỏ qua · {run.noQueryTermsCount > 0 ? 'JD chưa có từ khóa để tìm · ' : ''}phương pháp {run.methodVersion}.</p>
      {run.items.length ? <ol className="matching-results">{run.items.map(item => <li key={`${item.candidateId}:${item.resumeVersionId}`}>
        <div><strong>{item.displayName}</strong>{item.headline && <small>{item.headline}</small>}
          <small>CV v{item.versionNo} · retrieval_score {item.retrievalScore.toFixed(3)} · trùng {item.keywordOverlap.count}/{item.keywordOverlap.queryTermCount} từ khóa JD</small>
          {item.keywordOverlap.terms.length > 0 && <small>Từ khóa trùng: {item.keywordOverlap.terms.join(', ')}</small>}
          <small>{revisionDiagnosticText(item.confirmedRevisionMatch, 'recruiter')}</small>
        </div><a href={`/api/public/resumes/${item.candidateId}/${item.resumeVersionId}/file`} target="_blank" rel="noreferrer">Mở CV công khai</a>
      </li>)}</ol> : <p>Không có CV công khai đọc được với từ khóa trùng JD.</p>}
    </>}
  </section>;
}
