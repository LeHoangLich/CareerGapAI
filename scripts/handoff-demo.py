"""Windows localhost handoff demo: isolated persistent synthetic DB, all providers Fake.
Usage: python scripts/handoff-demo.py start|fault|stop
Requires Docker Desktop, .NET SDK, Node and npm ci in frontend. Never loads .env.
"""
from pathlib import Path
import sys,os,json,secrets,subprocess,time,urllib.request,shutil

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'.local/handoff-demo-20261029'
CONTAINER='careergap-handoff-demo-20261029'
OWNER='careergap.handoff=20261029'
URL='http://127.0.0.1:5190/'
MODE=sys.argv[1] if len(sys.argv)>1 else 'start'
if MODE not in ('start','fault','stop'):raise SystemExit(__doc__)
OUT.mkdir(parents=True,exist_ok=True)
flags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0
env={k:v for k,v in os.environ.items() if k.upper() in {'PATH','SYSTEMROOT','WINDIR','TEMP','TMP','USERPROFILE','APPDATA','LOCALAPPDATA','PROGRAMFILES','PROGRAMFILES(X86)','PROGRAMW6432','COMSPEC','PATHEXT','DOTNET_ROOT'}}
def save(p,j):p.write_text(json.dumps(j,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
def run(name,args,cwd=ROOT,e=None):
 with (OUT/(name+'.log')).open('ab') as log:
  p=subprocess.run(args,cwd=cwd,env=e or env,stdout=log,stderr=subprocess.STDOUT,creationflags=flags)
 if p.returncode:raise RuntimeError(name+' failed; see '+str(OUT/(name+'.log')))
def stop():
 state=OUT/'process.json'
 if state.exists():
  j=json.loads(state.read_text());pid=int(j['pid'])
  script=f"$p=Get-CimInstance Win32_Process -Filter 'ProcessId = {pid}'; if($p -and $p.CommandLine -like '*handoff-demo-20261029*CareerGap.Api.dll*'){{Stop-Process -Id {pid} -Force}}"
  subprocess.run(['powershell','-NoProfile','-Command',script],capture_output=True,creationflags=flags)
  j['stopped']=True;save(state,j)
if MODE=='stop':stop();print('Owned demo API stopped; synthetic database/container retained.');sys.exit()
cfgpath=OUT/'config.private.json'
if cfgpath.exists():cfg=json.loads(cfgpath.read_text())
else:
 cfg={'password':secrets.token_urlsafe(30),'accounts':{role.lower():{'email':f'handoff-{role.lower()}-{secrets.token_hex(4)}@example.invalid','password':secrets.token_urlsafe(22)+'Aa1!'} for role in ['Candidate','Recruiter','Admin']}}
 save(cfgpath,cfg)
stop()
inspection=subprocess.run(['docker','inspect',CONTAINER],env=env,capture_output=True,creationflags=flags)
if inspection.returncode:
 e=dict(env,POSTGRES_PASSWORD=cfg['password'])
 run('docker-create',['docker','run','-d','--name',CONTAINER,'--label',OWNER,'-p','127.0.0.1:55449:5432','-e','POSTGRES_USER=handoff_demo','-e','POSTGRES_PASSWORD','-e','POSTGRES_DB=careergap_test_ui_handoff_demo','-v','careergap-handoff-demo-20261029:/var/lib/postgresql/data','postgres:16-alpine'],e=e)
else:
 info=json.loads(inspection.stdout)[0]
 if info['Config'].get('Labels',{}).get('careergap.handoff')!='20261029':raise RuntimeError('Container ownership mismatch')
 run('docker-start',['docker','start',CONTAINER])
for _ in range(120):
 p=subprocess.run(['docker','exec','-e','PGPASSWORD',CONTAINER,'psql','-h','127.0.0.1','-U','handoff_demo','-d','careergap_test_ui_handoff_demo','-Atc','SELECT 1'],env=dict(env,PGPASSWORD=cfg['password']),capture_output=True,creationflags=flags,timeout=5)
 if p.returncode==0 and p.stdout.strip()==b'1':break
 time.sleep(.5)
else:raise RuntimeError('Owned database not ready')
e=dict(env,ASPNETCORE_ENVIRONMENT='Development',DOTNET_ENVIRONMENT='Development',ConnectionStrings__Database=f"Host=127.0.0.1;Port=55449;Database=careergap_test_ui_handoff_demo;Username=handoff_demo;Password={cfg['password']}",Storage__Provider='Local',Storage__Root=str(OUT/'files'),Ai__Provider='Fake',Ai__Enabled='false' if MODE=='fault' else 'true',Ai__ApiKey='',Ai__MonthlyLimit='0',Ai__PollSeconds='1',Ai__PrivateDiagnostics='false',Search__Provider='Fake',Search__ApiKey='',Email__Provider='Fake',Email__ApiKey='',Recovery__BaseUrl=URL,Logging__LogLevel__Default='Warning')
runtime=OUT/'runtime'
if not (runtime/'CareerGap.Api.dll').exists():
 if not (ROOT/'frontend/node_modules').exists():raise RuntimeError('Run npm ci in frontend first')
 npm=shutil.which('npm.cmd' if os.name=='nt' else 'npm')
 run('frontend-build',[npm,'run','build'],ROOT/'frontend')
 run('backend-restore',['dotnet','restore','backend/CareerGap.Api/CareerGap.Api.csproj','--locked-mode','--artifacts-path',str(OUT/'build')])
 run('backend-build',['dotnet','build','backend/CareerGap.Api/CareerGap.Api.csproj','--no-restore','--artifacts-path',str(OUT/'build')])
 shutil.copytree(OUT/'build/bin/CareerGap.Api/debug',runtime,ignore=shutil.ignore_patterns('appsettings.Local.json'))
 shutil.copytree(ROOT/'frontend/dist',runtime/'wwwroot')
if not cfg.get('migrated'):
 migrate=OUT/'migrate';migrate.mkdir(exist_ok=True)
 project=(ROOT/'backend/CareerGap.Api/CareerGap.Api.csproj').as_posix()
 (migrate/'Migrate.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><ProjectReference Include="'+project+'" /></ItemGroup></Project>')
 (migrate/'Program.cs').write_text('using CareerGap.Api.Data;using Microsoft.EntityFrameworkCore;using Npgsql;var c=Environment.GetEnvironmentVariable("ConnectionStrings__Database")!;var n=new NpgsqlConnectionStringBuilder(c);if(n.Host!="127.0.0.1"||n.Port!=55449||n.Database!="careergap_test_ui_handoff_demo")throw new Exception("isolated_only");await using var db=new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(c).Options);await db.Database.MigrateAsync();')
 run('migration-build',['dotnet','build',str(migrate/'Migrate.csproj'),'--artifacts-path',str(OUT/'build')])
 run('migration',['dotnet',str(OUT/'build/bin/Migrate/debug/Migrate.dll')],e=e)
 cfg['migrated']=True;save(cfgpath,cfg)
if not cfg.get('seeded'):
 for role,row in cfg['accounts'].items():run('seed-'+role,['dotnet',str(runtime/'CareerGap.Api.dll'),'--create-demo-user'],runtime,dict(e,DEMO_EMAIL=row['email'],DEMO_PASSWORD=row['password'],DEMO_ROLE=role.capitalize()))
 cfg['seeded']=True;save(cfgpath,cfg)
 accounts='# Tài khoản demo synthetic private\n\n'+''.join(f"## {role}\nEmail: {row['email']}\nMật khẩu: {row['password']}\n\n" for role,row in cfg['accounts'].items())
 (OUT/'accounts.private.md').write_text(accounts,encoding='utf-8')
with (OUT/'api.log').open('ab') as log:
 p=subprocess.Popen(['dotnet',str(runtime/'CareerGap.Api.dll'),'--urls',URL.rstrip('/')],cwd=runtime,env=e,stdout=log,stderr=subprocess.STDOUT,creationflags=flags)
save(OUT/'process.json',{'pid':p.pid,'mode':MODE,'providers':'Fake','real_keys_loaded':False,'URL':URL,'database':'isolated synthetic handoff demo','stopped':False})
for _ in range(100):
 if p.poll() is not None:raise RuntimeError('Demo exited; see private api.log')
 try:
  with urllib.request.urlopen(URL+'api/health',timeout=2) as r:
   if r.status==200:break
 except OSError:pass
 time.sleep(.2)
else:raise RuntimeError('Demo API not ready')
print('Demo ready '+URL+' ; all providers Fake, mode='+MODE)
print('Private accounts: '+str(OUT/'accounts.private.md'))
