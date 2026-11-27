import { useState } from 'react';
import { api } from './api';
import { Dialog, ValidatedForm } from './ui';

export function ReportButton({ targetType, targetId }: { targetType: 'job' | 'public_resume'; targetId: string }) {
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState('');
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function send() {
    setBusy(true);
    setError('');
    try {
      await api('/api/reports', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ targetType, targetId, reason: reason.trim() }) });
      setMessage('Đã gửi báo cáo tới quản trị viên.');
      setOpen(false);
      setReason('');
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <div>
      <button type="button" className="link-button" onClick={() => { setOpen(true); setError(''); setMessage(''); }}>Báo cáo nội dung</button>
      <Dialog open={open} title="Báo cáo nội dung" busy={busy} onClose={() => setOpen(false)}>
        <p>Ghi rõ vấn đề với {targetType === 'job' ? 'tin tuyển dụng' : 'CV công khai'} để quản trị viên xem xét.</p>
        <ValidatedForm onSubmit={e => { e.preventDefault(); if (reason.trim().length >= 3) void send(); }}>
          <label>Lý do báo cáo<textarea name="reportReason" required minLength={3} value={reason} maxLength={1000} onChange={e => setReason(e.target.value)} /></label>
          <div className="dialog-actions">
            <button type="button" className="secondary" disabled={busy} onClick={() => setOpen(false)}>Hủy</button>
            <button type="submit" disabled={busy || reason.trim().length < 3}>{busy ? 'Đang gửi…' : 'Gửi báo cáo'}</button>
          </div>
        </ValidatedForm>
        {error && <p className="error" role="alert">{error}</p>}
      </Dialog>
      {message && <p role="status">{message}</p>}
    </div>
  );
}
