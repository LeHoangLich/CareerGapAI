import { useEffect, useState } from 'react';
import { ReportButton } from './reportButton';
import { api, type PagedResponse } from './api';
import { sharedProfileHref } from './jobLinks';
import { Alert, ConfirmDialog, EmptyState, PageHeader, Pagination, StaticLoading } from './ui';

type Resume = { id: string; title: string; versions: { id: string; versionNo: number }[] };
type Selection = { candidateId: string; resumeVersionId: string; versionNo: number; status: 'ready' | 'not_ready'; rowVersion: number; publishedAt: string; redactionVerifiedAt: string | null };
type PublicResume = { candidateId: string; resumeVersionId: string; displayName: string; headline: string | null; versionNo: number; publishedAt: string; redactionStatus: string };
type Session = { authenticated: boolean; roles: string[] };

export function CandidatePublicResumeManager({ resumes, selectedVersionId }: { resumes: Resume[]; selectedVersionId: string }) {
  const [selection, setSelection] = useState<Selection | null>();
  const [busy, setBusy] = useState(false);
  const [confirmHide, setConfirmHide] = useState(false);
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
      setSelection(null); setConfirmHide(false); setMessage('CV đã được ẩn; đường dẫn công khai cũ không còn mở được.');
    } catch (e) { setError((e as Error).message); }
    finally { setBusy(false); }
  }
  return <section className="public-resume-controls" aria-label="CV công khai">
    <h3>Quyền công khai CV</h3>
    <p>Tải lên không tự công khai. Bạn chọn chính xác một phiên bản; recruiter được xem file gốc, candidate khác chỉ được xem bản dựng lại đã kiểm tra và ẩn liên hệ.</p>
    {selection ? <p className="hint" role="status">Đang công khai phiên bản {selection.versionNo} · {selection.status === 'ready' ? 'bản ẩn liên hệ đã sẵn sàng' : 'bản xem cho candidate khác chưa sẵn sàng'}.</p> : <p className="hint">Hiện chưa có phiên bản công khai.</p>}
    <div className="row public-resume-actions"><button type="button" disabled={busy || !selected} onClick={() => void publish()}>{busy ? 'Đang xử lý…' : `Công khai phiên bản ${selected?.versionNo ?? 'đã chọn'}`}</button>
      {selection && <button type="button" className="secondary" disabled={busy} onClick={() => setConfirmHide(true)}>Ẩn CV công khai</button>}</div>
    {message && <Alert tone="success">{message}</Alert>}{error && <Alert tone="error">{error}</Alert>}
    <ConfirmDialog
      open={confirmHide}
      title="Ẩn CV công khai"
      message="Ẩn CV công khai hiện tại? Đường dẫn công khai cũ sẽ không còn mở được."
      confirmLabel="Ẩn CV"
      busy={busy}
      onClose={() => setConfirmHide(false)}
      onConfirm={hide}
    />
  </section>;
}

export function PublicResumeBrowser({ session }: { session?: Session }) {
  const [items, setItems] = useState<PublicResume[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const isCandidate = session?.authenticated === true && session.roles.includes('Candidate');
  const isRecruiter = session?.authenticated === true && session.roles.includes('Recruiter');
  useEffect(() => {
    if (!isCandidate && !isRecruiter) { setItems([]); setTotal(0); return; }
    let cancelled = false;
    setLoading(true);
    api<PagedResponse<PublicResume>>(`/api/public/resumes?page=${page}`).then(value => {
      if (!cancelled) { setItems(value.items); setTotal(value.total); setError(''); }
    }).catch(e => { if (!cancelled) setError((e as Error).message); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [isCandidate, isRecruiter, page]);
  if (!isCandidate && !isRecruiter) return null;
  return <section className="card public-resume-browser" id="public-resumes">
    <PageHeader
      title="Kho CV công khai"
      description={isCandidate ? 'Bạn chỉ thấy CV có bản dẫn xuất đã kiểm tra. Các liên hệ, link và ảnh từ file gốc không được chuyển sang bản này.' : 'CV được ứng viên chủ động công khai. Nhà tuyển dụng có thể mở file gốc theo quyền của kho CV.'}
      actions={<span className="badge">{isCandidate ? 'Bản đã ẩn liên hệ' : 'Quyền nhà tuyển dụng'}</span>}
    />
    {error && <Alert tone="error">{error}</Alert>}
    {loading && items.length === 0 && <StaticLoading message="Đang tải kho CV công khai…"/>}
    <ul className="public-resume-list">{items.map(item => <li key={item.candidateId}>
      <div><strong>{item.displayName}</strong>{item.headline && <small>{item.headline}</small>}<small>CV phiên bản {item.versionNo} · công khai {new Date(item.publishedAt).toLocaleDateString('vi-VN')}</small></div>
      <a href={`/api/public/resumes/${item.candidateId}/${item.resumeVersionId}/file`} target="_blank" rel="noreferrer">Mở CV</a>
      <ReportButton targetType="public_resume" targetId={item.resumeVersionId}/>
      {isRecruiter && <a href={sharedProfileHref(item.candidateId)}>Xem hồ sơ được chia sẻ</a>}
      {isRecruiter && item.redactionStatus !== 'ready' && <small>Bản dành cho ứng viên khác chưa sẵn sàng.</small>}
    </li>)}</ul>
    {!loading && !items.length && !error && <EmptyState title="Chưa có CV công khai trong trang này."/>}
    <Pagination page={page} totalItems={total} pageSize={20} busy={loading} ariaLabel="Phân trang kho CV công khai" onPageChange={setPage}/>
  </section>;
}
