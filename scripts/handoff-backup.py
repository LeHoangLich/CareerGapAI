"""Offline snapshot/restore verification for the synthetic handoff demo only."""
from pathlib import Path
import sys,os,json,subprocess,hashlib,shutil,datetime
ROOT=Path(__file__).resolve().parents[1];OUT=ROOT/'.local/handoff-demo-20261029';C='careergap-handoff-demo-20261029';DB='careergap_test_ui_handoff_demo'
mode=sys.argv[1] if len(sys.argv)>1 else 'backup'
if mode not in ('backup','verify'):raise SystemExit('Usage: python scripts/handoff-backup.py backup|verify')
cfg=json.loads((OUT/'config.private.json').read_text());env=dict(os.environ,PGPASSWORD=cfg['password'])
def cmd(a):return subprocess.check_output(a,env=env,stderr=subprocess.PIPE)
owned=cmd(['docker','inspect','--format','{{index .Config.Labels "careergap.handoff"}}',C]).decode().strip();assert owned=='20261029'
def sql(q,db=DB):return cmd(['docker','exec','-e','PGPASSWORD',C,'psql','-h','127.0.0.1','-U','handoff_demo','-d',db,'-X','-Atc',q]).decode().strip()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def rows(db):
 names=json.loads(sql("SELECT json_agg(tablename ORDER BY tablename) FROM pg_tables WHERE schemaname='public'",db));out=[]
 for name in names:
  table='"'+name.replace('"','""')+'"';value=sql("SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text),'[]'::jsonb)::text FROM "+table+' t',db);out.append({'table':name,'sha256':hashlib.sha256(value.encode()).hexdigest()})
 return out
def objects():return [{'path':p.relative_to(OUT/'files').as_posix(),'sha256':sha(p)} for p in sorted((OUT/'files').rglob('*')) if p.is_file()]
def save(p,j):p.write_text(json.dumps(j,indent=2)+'\n')
if mode=='backup':
 subprocess.run([sys.executable,str(ROOT/'scripts/handoff-demo.py'),'stop'],check=True,capture_output=True)
 stamp=datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ');dest=OUT/'backups'/stamp;dest.mkdir(parents=True)
 before=rows(DB);obj=objects();dump='/tmp/careergap-handoff-'+stamp+'.dump'
 cmd(['docker','exec','-e','PGPASSWORD',C,'pg_dump','-h','127.0.0.1','-U','handoff_demo','-d',DB,'-Fc','-f',dump]);cmd(['docker','cp',C+':'+dump,str(dest/'database.dump')])
 shutil.copytree(OUT/'files',dest/'objects')
 assert before==rows(DB) and obj==objects(),'Source changed during offline backup'
 save(dest/'manifest.json',{'database':DB,'offline':True,'synthetic_only':True,'tables':before,'objects':obj,'dump_sha256':sha(dest/'database.dump'),'DP_keys_in_database':int(sql('SELECT count(*) FROM "DataProtectionKeys"'))})
 (OUT/'latest-backup.txt').write_text(str(dest));print('Offline synthetic backup saved:',dest)
else:
 backup=Path((OUT/'latest-backup.txt').read_text());assert backup.resolve().is_relative_to((OUT/'backups').resolve());m=json.loads((backup/'manifest.json').read_text());assert sha(backup/'database.dump')==m['dump_sha256']
 target='careergap_restore_handoff_'+datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%d%H%M%S');assert not sql("SELECT 1 FROM pg_database WHERE datname='"+target+"'")
 cmd(['docker','exec','-e','PGPASSWORD',C,'createdb','-h','127.0.0.1','-U','handoff_demo',target]);cmd(['docker','cp',str(backup/'database.dump'),C+':/tmp/handoff-verify.dump']);cmd(['docker','exec','-e','PGPASSWORD',C,'pg_restore','-h','127.0.0.1','-U','handoff_demo','-d',target,'--exit-on-error','/tmp/handoff-verify.dump'])
 assert rows(target)==m['tables'];assert int(sql('SELECT count(*) FROM "DataProtectionKeys"',target))==m['DP_keys_in_database']
 assert all(sha(backup/'objects'/x['path'])==x['sha256'] for x in m['objects'])
 receipt={'status':'passed','database':target,'tables':len(m['tables']),'objects':len(m['objects']),'data_protection_keys_match':True,'synthetic_only':True,'API_started_on_restored_data':False,'deletion_ledger_in_snapshot_preserved':True,'separate_post_backup_ledger_replay':'existing product CLI/previous E proof applies; no post-backup mutations in this isolated snapshot test','real_provider_calls':0}
 save(backup/'restore-verification.json',receipt);print('Restore matched:',len(m['tables']),'tables,',len(m['objects']),'objects and DP keys; restored DB remains offline.')
