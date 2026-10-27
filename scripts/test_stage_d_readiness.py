import copy, datetime as dt, importlib.util, json, pathlib, unittest
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('readiness',ROOT/'scripts/check-stage-d-readiness.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
NOW=dt.datetime(2026,10,23,tzinfo=dt.timezone.utc)
def ready():
    p=json.loads((ROOT/'project-docs/evaluation/stage-d-provider-readiness.template.json').read_text())
    p['outsideApplicationUsageReconciled']=True
    for name,provider in [('search','Tavily'),('email','Resend')]:
        p[name].update(provider=provider,backendKeyConfigured=True,allowanceConfirmed=True,periodStartUtc='2026-10-21T00:00:00Z',periodEndUtc='2026-11-20T00:00:00Z',verifiedAtUtc='2026-10-22T00:00:00Z')
    p['search']['remainingCreditsAcrossEnvironments']=1
    p['email'].update(senderVerified=True,recipientControlled=True,remainingDeliveriesAcrossEnvironments=1)
    return p
class ReadinessTests(unittest.TestCase):
    def test_pending_template_stays_disabled(self):
        p=json.loads((ROOT/'project-docs/evaluation/stage-d-provider-readiness.template.json').read_text());r=module.check(p,NOW);self.assertFalse(r['searchReady']);self.assertFalse(r['emailReady']);self.assertEqual(0,r['networkRequests'])
    def test_confirmed_offline_manifest(self):
        r=module.check(ready(),NOW);self.assertTrue(r['searchReady']);self.assertTrue(r['emailReady'])
    def test_other_database_is_not_new_allowance(self):
        p=ready();p['outsideApplicationUsageReconciled']=False;r=module.check(p,NOW);self.assertFalse(r['searchReady']);self.assertFalse(r['emailReady'])
    def test_query_must_not_include_cv_or_identity(self):
        p=ready();p['search']['queryFields'].append('cvText');self.assertFalse(module.check(p,NOW)['searchReady'])
    def test_cache_requires_terms_and_explicit_bounded_ttl(self):
        p=ready();p['search']['cacheMetadataMinutes']=5;self.assertFalse(module.check(p,NOW)['searchReady']);p['search']['cacheTermsReviewed']=True;self.assertTrue(module.check(p,NOW)['searchReady']);p['search']['cacheMetadataMinutes']=61;self.assertFalse(module.check(p,NOW)['searchReady'])
    def test_missing_shared_capacity_or_expired_window(self):
        for section,field,result in [('search','remainingCreditsAcrossEnvironments','searchReady'),('email','remainingDeliveriesAcrossEnvironments','emailReady')]:
            p=ready();p[section][field]=0;self.assertFalse(module.check(p,NOW)[result]);p=ready();p[section]['periodEndUtc']='2026-10-22T00:00:00Z';self.assertFalse(module.check(p,NOW)[result])
    def test_controlled_recipient_required(self):
        p=ready();p['email']['recipientControlled']=False;self.assertFalse(module.check(p,NOW)['emailReady'])
    def test_reject_secret_and_per_environment_scope(self):
        p=ready();p['search']['apiKey']='synthetic';self.assertRaises(ValueError,module.check,p,NOW);p=ready();p['scope']='per_database';self.assertRaises(ValueError,module.check,p,NOW)
if __name__=='__main__':unittest.main()
