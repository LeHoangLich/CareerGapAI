import { useEffect, useState } from 'react';
import { api } from './api';

type Resume = { id: string; title: string; versions: { id: string; versionNo: number }[] };
type Selection = { candidateId: string; resumeVersionId: string; versionNo: number; status: 'ready' | 'not_ready'; rowVersion: number; publishedAt: string; redactionVerifiedAt: string | null };
type PublicResume = { candidateId: string; resumeVersionId: string; displayName: string; headline: string | null; versionNo: number; publishedAt: string; redactionStatus: string };
type Session = { authenticated: boolean; roles: string[] };

export function CandidatePublicResumeManager({ resumes, selectedVersionId }: { resumes: Resume[]; selectedVersionId: string }) {
  const [selection, setSelection] = useState<Selection | null>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  useEffect(() => { api<Selection | null>('/api/resumes/public/me').then(setSelection).catch(e => setError((e as Error).message)); }, []);
  const selected = resumes.flatMap(resume => resume.versions).find(version => version.id === selectedVersionId);
  async function publish() {
    if (!selected) return;
    setBusy(true); setError(''); setMessage('');
    try {
      const saved = await api<Selection>('/api/resumes/public', { method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ resumeVersionId: selected.id, expectedRowVersion: selection?.rowVersion ?? null }) });
      setSelection(saved);
      setMessage(saved.status === 'ready' ? 'CV đã công khai. Candidate khác chỉ nhận bản đã ẩn liên hệ.' : 'CV đã chọn công khai cho recruiter; bản xem của candidate khác chưa sẵn sàng nên hệ thống không cung cấp file gốc.');
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  async function hide() {
    if (!selection) return;
    setBusy(true); setError(''); setMessage('');
    try {
      await api(`/api/resumes/public/me?rowVersion=${selection.rowVersion}`, { method: 'DELETE' });
      setSelection(null); setMessage('CV đã được ẩn; đường dẫn công khai cũ không còn mở được.');
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <section className="public-resume-controls" aria-label="CV công khai">
    <h3>Quyền công khai CV</h3>
    <p>Tải lên không tự công khai. Bạn chọn chính xác một phiên bản; recruiter được xem file gốc, candidate khác chỉ được xem bản dựng lại đã kiểm tra và ẩn liên hệ.</p>
    {selection ? <p className="hint" role="status">Đang công khai phiên bản {selection.versionNo} · {selection.status === 'ready' ? 'bản ẩn liên hệ đã sẵn sàng' : 'bản xem cho candidate khác chưa sẵn sàng'}.</p> : <p className="hint">Hiện chưa có phiên bản công khai.</p>}
    <div className="row public-resume-actions"><button disabled={busy || !selected} onClick={() => void publish()}>{busy ? 'Đang xử lý…' : selection ? `Công khai phiên bản ${selected?.versionNo ?? 'đã chọn'}` : `Công khai phiên bản ${selected?.versionNo ?? 'đã chọn'}`}</button>
      {selection && <button className="secondary" disabled={busy} onClick={() => void hide()}>Ẩn CV công khai</button>}</div>
    {message && <p role="status">{message}</p>}{error && <p role="alert" className="error">{error}</p>}
  </section>;
}

export function PublicResumeBrowser({ session }: { session?: Session }) {
  const [items, setItems] = useState<PublicResume[]>([]);
  const [error, setError] = useState('');
  const [page, setPage] = useState(1);
  const [hasMore, setHasMore] = useState(false);
  const isCandidate = session?.authenticated === true && session.roles.includes('Candidate');
  const isRecruiter = session?.authenticated === true && session.roles.includes('Recruiter');
  useEffect(() => {
    if (!isCandidate && !isRecruiter) { setItems([]); return; }
    let cancelled = false;
    api<PublicResume[]>(`/api/public/resumes?page=${page}`).then(value => {
      if (!cancelled) { setItems(value); setHasMore(value.length === 20); setError(''); }
    }).catch(e => { if (!cancelled) setError((e as Error).message); });
    return () => { cancelled = true; };
  }, [isCandidate, isRecruiter, page]);
  if (!isCandidate && !isRecruiter) return null;
  return <section className="card public-resume-browser"><div className="section-heading"><h2>Kho CV công khai</h2><span className="badge">{isCandidate ? 'Bản đã ẩn liên hệ' : 'Quyền recruiter'}</span></div>
    <p>{isCandidate ? 'Bạn chỉ thấy CV có bản dẫn xuất đã kiểm tra. Các liên hệ, link và ảnh từ file gốc không được chuyển sang bản này.' : 'CV được ứng viên chủ động công khai. Recruiter có thể mở file gốc theo quyền của kho CV.'}</p>
    {error && <p role="alert" className="error">{error}</p>}
    <ul className="public-resume-list">{items.map(item => <li key={item.candidateId}>
      <div><strong>{item.displayName}</strong>{item.headline && <small>{item.headline}</small>}<small>CV phiên bản {item.versionNo} · công khai {new Date(item.publishedAt).toLocaleDateString('vi-VN')}</small></div>
      <a href={`/api/public/resumes/${item.candidateId}/${item.resumeVersionId}/file`} target="_blank" rel="noreferrer">Mở CV</a>
      {isRecruiter && item.redactionStatus !== 'ready' && <small>Bản dành cho candidate khác chưa sẵn sàng.</small>}
    </li>)}</ul>
    {!items.length && !error && <p>Chưa có CV công khai trong trang này.</p>}
    <div className="row public-resume-pages"><button className="secondary" disabled={page === 1} onClick={() => setPage(value => value - 1)}>Trước</button><span>Trang {page}</span><button className="secondary" disabled={!hasMore} onClick={() => setPage(value => value + 1)}>Sau</button></div>
  </section>;
}
