"""Offline search/email admission checklist. Never reads a key or sends a request."""
import argparse
import datetime as dt
import hashlib
import json
from pathlib import Path

UTC = dt.timezone.utc

def check(plan, now=None):
    now = now or dt.datetime.now(UTC)
    def no_secret(value):
        if isinstance(value, dict):
            if any(k.casefold().replace('_', '') in {'apikey', 'password', 'token', 'secret'} for k in value):
                raise ValueError('secret_field_forbidden')
            for item in value.values(): no_secret(item)
        elif isinstance(value, list):
            for item in value: no_secret(item)
    no_secret(plan)
    if plan.get('schemaVersion') != 1 or plan.get('scope') != 'whole_development_all_environments':
        raise ValueError('invalid_manifest_scope')
    def window(section):
        try:
            start, end, verified = (dt.datetime.fromisoformat(section[k].replace('Z', '+00:00')) for k in ('periodStartUtc', 'periodEndUtc', 'verifiedAtUtc'))
            return all(x.utcoffset() == dt.timedelta(0) for x in (start, end, verified)) and start <= now < end and dt.timedelta(0) < end-start <= dt.timedelta(days=32) and now-dt.timedelta(days=31) < verified <= now
        except (KeyError, ValueError, TypeError, AttributeError): return False
    def capacity(section, remaining, planned, unit=1):
        a,b=section.get(remaining),section.get(planned)
        return type(a) is int and type(b) is int and a >= 0 and b > 0 and b*unit <= a
    base = plan.get('outsideApplicationUsageReconciled') is True
    search, email = plan.get('search', {}), plan.get('email', {})
    search_reasons=[]; email_reasons=[]
    for section,reasons,provider in ((search,search_reasons,'Tavily'),(email,email_reasons,'Resend')):
        if not base: reasons.append('whole_account_usage_not_reconciled')
        if section.get('provider') != provider: reasons.append('provider_not_confirmed_for_prepared_adapter')
        for field in ('backendKeyConfigured','allowanceConfirmed'):
            if section.get(field) is not True: reasons.append(field+'_required')
        if not window(section): reasons.append('current_verified_utc_window_required')
    if search.get('mode') != 'basic' or search.get('creditsPerRequest') != 1: search_reasons.append('basic_pricing_requires_verification')
    if not capacity(search,'remainingCreditsAcrossEnvironments','plannedRequests'): search_reasons.append('insufficient_shared_credits')
    if search.get('queryFields') != ['topic','level','language']: search_reasons.append('query_privacy_contract_required')
    cache=search.get('cacheMetadataMinutes')
    if type(cache) is not int or not 0 <= cache <= 60 or cache > 0 and search.get('cacheTermsReviewed') is not True: search_reasons.append('cache_terms_or_ttl_invalid')
    for field in ('senderVerified','recipientControlled'):
        if email.get(field) is not True: email_reasons.append(field+'_required')
    if not capacity(email,'remainingDeliveriesAcrossEnvironments','plannedDeliveries'): email_reasons.append('insufficient_shared_deliveries')
    return {'searchReady':not search_reasons,'emailReady':not email_reasons,'searchReasons':search_reasons,'emailReasons':email_reasons,'networkRequests':0,'configurationProofOnly':True}

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('manifest',type=Path);args=parser.parse_args()
    try:
        raw=args.manifest.read_bytes();result=check(json.loads(raw.decode('utf-8-sig')));result['manifestSha256']=hashlib.sha256(raw).hexdigest()
    except (OSError, ValueError, TypeError, AttributeError):
        print(json.dumps({'error':'invalid_manifest_or_secret_field','networkRequests':0}));return 1
    print(json.dumps(result,ensure_ascii=False,indent=2));return 0 if result['searchReady'] and result['emailReady'] else 2

if __name__=='__main__':raise SystemExit(main())
