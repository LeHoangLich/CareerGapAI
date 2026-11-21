const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function jobHref(id: string) {
  return `#job?id=${encodeURIComponent(id.toLowerCase())}`;
}

export function jobIdFromHash(hash: string): string | null {
  if (hash.split('?')[0] !== '#job') return null;
  const id = new URLSearchParams(hash.slice(hash.indexOf('?') + 1)).get('id');
  return id && guid.test(id) ? id.toLowerCase() : null;
}

export function jobShareUrl(id: string, currentUrl: string): string {
  const url = new URL(currentUrl);
  // Share the public job only; never copy reset tokens or other page query data.
  url.search = '';
  url.hash = jobHref(id);
  return url.href;
}

export function jobSignInHref(id: string) {
  return `#access?returnTo=${encodeURIComponent(jobHref(id))}`;
}

export function jobReturnAfterSignIn(hash: string): string | null {
  if (hash.split('?')[0] !== '#access') return null;
  const target = new URLSearchParams(hash.slice(hash.indexOf('?') + 1)).get('returnTo');
  const id = target && jobIdFromHash(target);
  return id ? jobHref(id) : null;
}
