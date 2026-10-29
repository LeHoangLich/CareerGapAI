import { useEffect, useId, useRef, useState } from 'react';
import { api } from './api';
import { AiBadge, FieldError } from './ui';
import type { PdfLanguage } from './resumeBuilderPdf';

export type WritingEdit = { target: string; text: string };
type Proposal = { id: string; status: string; errorCode: string | null; provider: string; resumeVersionId: string; resumeRevisionId: string; sections: (WritingEdit & { sources: { key: string; value: string; sourceType: string }[] })[] | null; decision: string | null; acceptedSections: WritingEdit[] | null };
const label = (target: string) => target === 'summary' ? 'Giới thiệu' : target.startsWith('experience') ? `Kinh nghiệm ${Number(target.match(/\d+/)?.[0] ?? 0) + 1}` : `Dự án ${Number(target.match(/\d+/)?.[0] ?? 0) + 1}`;
export function WritingPanel({ versionId, disabled, onApply, language, onLanguageChange }: { versionId: string; disabled: boolean; onApply: (edits: WritingEdit[], revisionId: string) => boolean; language: PdfLanguage; onLanguageChange: (language: PdfLanguage) => void }) {
  const fieldErrorPrefix = useId();
  const [proposal, setProposal] = useState<Proposal>();
  const [applied, setApplied] = useState(false);
  const [edits, setEdits] = useState<WritingEdit[]>([]); const [busy, setBusy] = useState(false); const [error, setError] = useState('');
  const live = useRef(true);
  const pendingKey = useRef<string | undefined>(undefined);
  useEffect(() => { live.current = true; return () => { live.current = false; }; }, []);
  useEffect(() => { let disposed = false; void api<{proposal: Proposal | null}>(`/api/writing/proposals?resumeVersionId=${versionId}`).then(({proposal:value}) => { if (!disposed && value) { setProposal(value); if(value.sections)setEdits(value.sections.map(({target,text})=>({target,text}))); } }).catch(e=>{if(!disposed)setError((e as Error).message)}); return ()=>{disposed=true}; }, [versionId]);
  useEffect(() => {
    if (!proposal || !['queued', 'running'].includes(proposal.status)) return;
    let disposed = false;
    const timer = setTimeout(() => {
      void api<Proposal>(`/api/writing/proposals/${proposal.id}`).then(value => { if (!disposed) { setProposal(value); if (value.sections) setEdits(value.sections.map(({ target, text }) => ({ target, text }))); } }).catch(e => { if (!disposed) setError((e as Error).message); });
    }, 1800);
    return () => { disposed = true; clearTimeout(timer); };
  }, [proposal]);
  async function create() {
    setBusy(true); setError(''); setProposal(undefined); setEdits([]); setApplied(false);
    try {
      pendingKey.current ??= crypto.randomUUID();
      const job = await api<{ id: string }>('/api/writing/proposals', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ resumeVersionId: versionId, language, idempotencyKey: pendingKey.current }) });
      pendingKey.current = undefined;
      const value = await api<Proposal>(`/api/writing/proposals/${job.id}`); if (live.current) { setProposal(value); if (value.sections) setEdits(value.sections.map(({ target, text }) => ({ target, text }))); }
    } catch (e) { if (live.current) setError((e as Error).message); } finally { if (live.current) setBusy(false); }
  }
  async function decide(action: 'accept' | 'discard') {
    if (!proposal) return; setBusy(true); setError('');
    try {
      const decision = await api<{ sections: WritingEdit[] }>(`/api/writing/proposals/${proposal.id}/decision`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ action, sections: action === 'accept' ? edits : undefined }) });
      if (!live.current) return;
      setProposal({ ...proposal, decision: action, acceptedSections: action === 'accept' ? decision.sections : null });
      if (action === 'accept') setApplied(onApply(decision.sections, proposal.resumeRevisionId));
    } catch (e) { if (live.current) setError((e as Error).message); } finally { if (live.current) setBusy(false); }
  }
  const running = proposal && ['queued', 'running'].includes(proposal.status);
  return <section className="writing-panel" aria-label="AI hỗ trợ soạn CV"><h3>AI hỗ trợ soạn CV từ dữ kiện đã xác nhận</h3>
    <p>AI chọn và sắp xếp dữ kiện cho phần giới thiệu, kinh nghiệm và dự án. Nội dung nguồn được giữ nguyên; nhãn trình bày Việt–Anh được chọn bên dưới. Bạn sửa, duyệt rồi mới đưa vào bản nháp; sau đó kiểm tra PDF và lưu phiên bản mới.</p>
    <label>Ngôn ngữ trình bày<select aria-label="Ngôn ngữ trình bày" disabled={disabled || busy || !!running} value={language} onChange={e => onLanguageChange(e.target.value as PdfLanguage)}><option value="vi">Tiếng Việt</option><option value="en">English</option></select></label>
    <button type="button" disabled={disabled || busy || !!running} onClick={() => void create()}>{running ? 'Đang chuẩn bị đề xuất…' : 'Tạo đề xuất soạn CV'}</button>
    {proposal?.status === 'succeeded' && <>{proposal.decision !== 'discard' && <AiBadge accepted={applied}>{applied ? 'Đã duyệt và đưa vào bản nháp' : proposal.decision === 'accept' ? 'Đã duyệt · chờ đưa vào bản nháp' : 'AI đề xuất · chờ bạn duyệt'}</AiBadge>}<p>{proposal.provider === 'Fake' ? 'Kết quả mô phỏng kiểm thử.' : 'Đề xuất từ AI.'} Hãy kiểm tra thông tin trước khi duyệt.</p>
      {proposal.sections?.map((section, index) => <div key={section.target}><label>{label(section.target)}<textarea aria-label={label(section.target)} aria-invalid={!edits[index]?.text.trim() ? true : undefined} aria-describedby={!edits[index]?.text.trim() ? `${fieldErrorPrefix}-${index}` : undefined} disabled={!!proposal.decision || busy} maxLength={section.target === 'summary' ? 3000 : 1200} value={edits[index]?.text ?? section.text} onChange={e => { const text = e.target.value; setEdits(current => current.map((item, i) => i === index ? { ...item, text } : item)); }}/></label><FieldError id={`${fieldErrorPrefix}-${index}`} message={!edits[index]?.text.trim() ? "Hãy nhập nội dung trước khi duyệt." : ""}/><details><summary>Dữ kiện dùng cho đề xuất</summary>{section.sources.map(source => <blockquote key={source.key}>{source.value}<small>{source.sourceType === 'user_statement' ? 'Thông tin tự khai đã xác nhận' : 'Nội dung CV đã xác nhận'}</small></blockquote>)}</details></div>)}
      {!proposal.decision ? <div className="row"><button type="button" disabled={busy || disabled || edits.some(e => !e.text.trim())} onClick={() => void decide('accept')}>Chấp nhận và đưa vào bản nháp</button><button type="button" className="secondary" disabled={busy} onClick={() => void decide('discard')}>Bỏ đề xuất</button></div> : <p role="status">{proposal.decision === 'accept' ? 'Đã duyệt. Kiểm tra bản nháp bên dưới trước khi tạo PDF và lưu.' : 'Đã bỏ đề xuất; CV giữ nguyên.'}</p>}
    </>}
    {proposal?.status === 'failed' && <p role="alert">Chưa tạo được đề xuất hợp lệ. CV của bạn chưa thay đổi.</p>}{error && <p role="alert">{error}</p>}
    {proposal?.decision === 'accept' && proposal.acceptedSections && <button type="button" className="secondary" disabled={busy || disabled} onClick={()=>setApplied(onApply(proposal.acceptedSections!, proposal.resumeRevisionId))}>Đưa bản đã duyệt vào bản nháp</button>}
    {running && error && <button type="button" className="secondary" onClick={()=>{setError('');setProposal({...proposal})}}>Kiểm tra lại đề xuất</button>}
  </section>;
}
