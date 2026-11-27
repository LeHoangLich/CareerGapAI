import { useEffect, useState } from 'react';
import { Bell, Briefcase } from '@phosphor-icons/react';
import { useRecruitmentLive, recruitmentConnection, recruitmentDisconnected, recruitmentSignal } from './recruitmentLive';

type BadgeSession = { authenticated: boolean; userId?: string; roles: string[]; deletionPending?: boolean };

export function CommunityBadge({ session }: { session?: BadgeSession }) {
  const [unread, setUnread] = useState(0);
  const recruitment = useRecruitmentLive(session?.userId);
  useEffect(() => {
    if (!session?.authenticated || session.deletionPending || !session.userId) return;
    const uid = session.userId;
    recruitmentConnection(uid, 'connecting');
    let stream: EventSource | undefined;
    let disposed = false;
    let initialized = false;
    let lastCommunity = '';
    const update = (event: MessageEvent<string>) => {
      if (disposed) return;
      try {
        const value = JSON.parse(event.data) as { unread: number; posts: unknown; comments: unknown; count: unknown; members: unknown; recruitment: unknown };
        if (Number.isSafeInteger(value.unread) && value.unread >= 0) setUnread(value.unread);
        recruitmentSignal(uid, value.recruitment);
        const community = JSON.stringify([value.unread, value.posts, value.comments, value.count, value.members]);
        if (initialized && community !== lastCommunity) window.dispatchEvent(new CustomEvent('community:update'));
        lastCommunity = community;
        initialized = true;
      } catch {
        /* Keep the last valid frame during reconnects. */
      }
    };
    const connect = () => {
      if (disposed) return;
      stream?.close();
      recruitmentConnection(uid, 'connecting');
      stream = new EventSource('/api/community/events');
      stream.onerror = () => { if (!disposed) recruitmentConnection(uid, 'reconnecting'); };
      stream.addEventListener('update', update as EventListener);
    };
    const offline = () => { stream?.close(); recruitmentConnection(uid, 'reconnecting'); };
    window.addEventListener('online', connect);
    window.addEventListener('offline', offline);
    if (navigator.onLine) connect();
    else offline();
    return () => {
      disposed = true;
      window.removeEventListener('online', connect);
      window.removeEventListener('offline', offline);
      stream?.close();
      recruitmentDisconnected(uid);
    };
  }, [session?.authenticated, session?.userId, session?.deletionPending]);
  const liveLabel = recruitment.connection === 'connected' ? 'Cập nhật trực tiếp' : recruitment.connection === 'reconnecting' ? 'Đang kết nối lại' : 'Đang kết nối';
  return session?.authenticated && !session.deletionPending ? (
    <div className="social-live-status" role="status" aria-atomic="true">
      <a href="#community?tab=notifications" className="badge social-count" aria-label={`Cộng đồng: ${unread} thông báo chưa đọc`} title="Thông báo cộng đồng">
        <Bell size={17} aria-hidden="true" />
        {unread > 0 && <span aria-hidden="true">{unread > 99 ? '99+' : unread}</span>}
      </a>
      <a href="#notifications" className="badge social-count" aria-label={`Tuyển dụng: ${recruitment.unread} thông báo chưa đọc`} title={`Thông báo tuyển dụng · ${liveLabel}`}>
        <Briefcase size={17} aria-hidden="true" />
        {recruitment.unread > 0 && <span aria-hidden="true">{recruitment.unread > 99 ? '99+' : recruitment.unread}</span>}
      </a>
    </div>
  ) : null;
}
