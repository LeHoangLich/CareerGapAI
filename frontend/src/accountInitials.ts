export type AccountSession = {
  authenticated: boolean;
  userId?: string;
  email?: string;
  roles: string[];
  displayName?: string | null;
  avatarImageId?: string | null;
  avatarUrl?: string | null;
  emailConfirmed?: boolean;
  lastLoginAt?: string | null;
  notifyRecruitment?: boolean;
  notifyCommunity?: boolean;
  deletionPending?: boolean;
  purgeAfter?: string;
};

export function accountInitials(displayName?: string | null, email?: string | null): string {
  const source = (displayName ?? '').trim();
  if (source.length > 0) {
    const parts = source.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return source.slice(0, 2).toUpperCase();
  }
  const mail = (email ?? '').trim();
  return mail ? mail.slice(0, 2).toUpperCase() : 'CG';
}
