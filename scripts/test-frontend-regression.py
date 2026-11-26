"""Isolated local frontend/backend regression. No dotenv, demo DB or real providers.

Run from the repository: python scripts/test-frontend-regression.py
Use --tests accounts.spec.ts ... for a targeted E2E run, --skip-backend to skip
the backend suite, or --skip-frontend to skip frontend static checks.
Installs the matching Playwright Chromium if needed and checks browser launch
before starting Docker. All generated data/logs stay in a unique
.local/frontend-step3-* directory.
"""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import secrets
import shutil
import socket
import subprocess
import sys
import time
import urllib.request
from xml.sax.saxutils import escape
from zipfile import ZipFile, ZIP_DEFLATED

ROOT = Path(__file__).resolve().parents[1]


def wait_for_postgres(container, database, *, env, creation_flags, log_path,
                      timeout=60, run_command=None, sleep=None, clock=None):
    """Require the final TCP server and a working authenticated database query.

    The image's temporary initialization server accepts Unix-socket probes before
    it shuts down. It must not satisfy the application readiness condition.
    """
    run_command = run_command or subprocess.run
    sleep = sleep or time.sleep
    clock = clock or time.monotonic
    probe_env = {**env, 'PGPASSWORD': env['POSTGRES_PASSWORD']}
    command = ['docker', 'exec', '-e', 'PGPASSWORD', container, 'psql',
               '-h', '127.0.0.1', '-U', 'ui_test', '-d', database,
               '-X', '-v', 'ON_ERROR_STOP=1', '-Atc', 'SELECT current_database(), 1']
    deadline = clock() + timeout
    attempts = 0
    with log_path.open('w', encoding='utf-8') as log:
        while clock() < deadline:
            attempts += 1
            try:
                probe = run_command(command, env=probe_env, capture_output=True,
                                    text=True, creationflags=creation_flags,
                                    timeout=min(5, max(.01, deadline - clock())))
                log.write(f'attempt={attempts} exit={probe.returncode}\n')
                log.write(probe.stdout + probe.stderr)
                log.flush()
                if probe.returncode == 0 and probe.stdout.strip() == f'{database}|1':
                    return attempts
            except subprocess.TimeoutExpired:
                log.write(f'attempt={attempts} probe_timeout\n')
                log.flush()
            sleep(min(.25, max(0, deadline - clock())))
    raise RuntimeError(f'Test PostgreSQL TCP/SQL readiness timed out; see {log_path}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tests', nargs='*', default=[])
    parser.add_argument('--skip-backend', action='store_true')
    parser.add_argument('--skip-frontend', action='store_true')
    args = parser.parse_args()
    stamp = dt.datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + secrets.token_hex(2)
    out = ROOT / '.local' / ('frontend-step3-' + stamp)
    out.mkdir(parents=True)
    container = 'careergap-ui-' + stamp
    database = 'careergap_test_ui_' + stamp.replace('-', '_')
    allowed = {'PATH', 'SYSTEMROOT', 'WINDIR', 'TEMP', 'TMP', 'DOTNET_ROOT', 'USERPROFILE', 'APPDATA', 'LOCALAPPDATA', 'PROGRAMFILES', 'PROGRAMFILES(X86)', 'PROGRAMW6432', 'COMSPEC', 'PATHEXT', 'HOME', 'PLAYWRIGHT_BROWSERS_PATH'}
    env = {key: value for key, value in os.environ.items() if key.upper() in allowed}
    ports = set()
    def free_port():
        with socket.socket() as sock:
            sock.bind(('127.0.0.1', 0))
            port = sock.getsockname()[1]
        if port in ports:
            return free_port()
        ports.add(port)
        return port
    pg, api_port, vite_port = free_port(), free_port(), free_port()
    api_url, ui_url = f'http://127.0.0.1:{api_port}', f'http://127.0.0.1:{vite_port}'
    password = secrets.token_urlsafe(24)
    connection = f'Host=127.0.0.1;Port={pg};Database={database};Username=ui_test;Password={password}'
    env.update(ASPNETCORE_ENVIRONMENT='Testing', DOTNET_ENVIRONMENT='Testing',
        ConnectionStrings__Database=connection, CAREERGAP_TEST_DATABASE=connection,
        Storage__Provider='Local', Storage__Root=str(out / 'files'),
        Ai__Provider='Fake', Ai__Enabled='true', Ai__ApiKey='', Ai__MonthlyLimit='0',
        Ai__PollSeconds='1', Ai__PrivateDiagnostics='false',
        Search__Provider='Fake', Search__ApiKey='', Email__Provider='Fake', Email__ApiKey='',
        Recovery__BaseUrl=ui_url + '/', Logging__LogLevel__Default='Warning',
        CAREERGAP_BASE_URL=ui_url, CAREERGAP_E2E_EVIDENCE_DIRECTORY=str(out / 'evidence'),
        CAREERGAP_E2E_OUTPUT_DIRECTORY=str(out / 'playwright'),
        CAREERGAP_E2E_JSON=str(out / 'e2e.json'),
        DEMO_EMAIL='foundation-' + stamp + '@example.invalid', DEMO_PASSWORD=secrets.token_urlsafe(24) + 'Aa1!',
        POSTGRES_USER='ui_test', POSTGRES_PASSWORD=password, POSTGRES_DB=database)
    results = {'run': stamp, 'directory': str(out), 'providers': 'Fake', 'dotenvLoaded': False, 'checks': {}, 'completed': False, 'cleanup': False}
    inventory = {}
    for pattern in ['frontend/src/*', 'frontend/e2e/*.ts', 'backend/CareerGap.Api/**/*.cs', 'tests/CareerGap.Api.Tests/**/*.cs', 'scripts/test-frontend-regression.py', 'scripts/test_frontend_regression.py', 'frontend/playwright.config.ts']:
        for file in ROOT.glob(pattern):
            if file.is_file() and not {'bin', 'obj'} & set(file.relative_to(ROOT).parts):
                inventory[file.relative_to(ROOT).as_posix()] = hashlib.sha256(file.read_bytes()).hexdigest()
    (out / 'source-sha256.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
    processes = []
    created = False
    creation_flags = subprocess.CREATE_NO_WINDOW if os.name == 'nt' else 0
    def save():
        (out / 'result.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
    def run(name, command, cwd=ROOT, check=True, command_env=None):
        with (out / (name + '.log')).open('w', encoding='utf-8') as log:
            completed = subprocess.run(command, cwd=cwd, env=env if command_env is None else command_env, stdout=log, stderr=subprocess.STDOUT, creationflags=creation_flags)
        results['checks'][name] = completed.returncode
        save()
        print(f'{name}: exit {completed.returncode}', flush=True)
        if check and completed.returncode:
            raise RuntimeError(f'{name} failed; see {out / (name + ".log")}')
        return completed.returncode
    def start(name, command, cwd):
        log = (out / (name + '.log')).open('w', encoding='utf-8')
        process = subprocess.Popen(command, cwd=cwd, env=env, stdout=log, stderr=subprocess.STDOUT, creationflags=creation_flags)
        processes.append((process, log))
        return process
    def ready(url, process):
        for _ in range(150):
            if process.poll() is not None:
                raise RuntimeError('Owned test service exited before readiness; see run logs')
            try:
                with urllib.request.urlopen(url, timeout=2) as response:
                    if response.status == 200:
                        return
            except OSError:
                pass
            time.sleep(.2)
        raise RuntimeError('Owned test service readiness timed out')
    def docx(path, lines):
        with ZipFile(path, 'w', ZIP_DEFLATED) as archive:
            archive.writestr('[Content_Types].xml', '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
            archive.writestr('_rels/.rels', '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
            archive.writestr('word/document.xml', '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>' + ''.join('<w:p><w:r><w:t>' + escape(line) + '</w:t></w:r></w:p>' for line in lines) + '</w:body></w:document>')
    docx(out / 'synthetic.docx', ['DỮ LIỆU GIẢ - Hồ sơ thử nghiệm', 'Kỹ năng: React, TypeScript, PostgreSQL.', 'Dự án: Xây dựng ứng dụng quản lý công việc.', 'Kinh nghiệm: Docker và kiểm thử dữ liệu.'])
    fields = {'displayName': 'Ứng Viên Tổng Hợp', 'email': 'synthetic@example.invalid', 'phone': '+84 900 000 001', 'location': 'Hồ Chí Minh', 'headline': 'Kỹ sư phần mềm', 'summary': 'Xây dựng dữ liệu thử.', 'skills[0]': 'React', 'skills[1]': 'TypeScript', 'skills[2]': 'PostgreSQL', 'experience[0].role': 'Developer', 'experience[0].organization': 'Synthetic Company', 'experience[0].period': '2024-2026', 'experience[0].description': 'Built test software.', 'projects[0].name': 'Test project', 'projects[0].context': 'Synthetic demo', 'projects[0].description': 'Built a fixture.', 'education[0].program': 'Computer Science', 'education[0].institution': 'Synthetic University', 'education[0].period': '2020-2024', 'education[0].details': 'Synthetic study.', 'certifications[0]': 'Synthetic Certificate'}
    docx(out / 'full-cv.docx', [f'{key}: {value}' for key, value in fields.items()])
    env.update(CAREERGAP_DOCX_FIXTURE=str(out / 'synthetic.docx'), CAREERGAP_FULL_CV_FIXTURE=str(out / 'full-cv.docx'))
    build = out / 'build'
    npm = shutil.which('npm.cmd' if os.name == 'nt' else 'npm')
    if not npm:
        raise RuntimeError('npm is required')
    print(str(out), flush=True)
    try:
        run('runner-tests', [sys.executable, '-m', 'unittest', 'discover', '-s', 'scripts', '-p', 'test_frontend_regression.py'])
        # Use the project's pinned CLI, so installation and E2E require the same
        # Chromium revision. The installer reuses an existing matching cache.
        node = shutil.which('node')
        if not node:
            raise RuntimeError('Node.js is required')
        run('playwright-install', [node, str(ROOT / 'frontend/node_modules/playwright/cli.js'),
            'install', 'chromium'], ROOT / 'frontend')
        run('playwright-browser', [node, '-e',
            "(async()=>{const {chromium}=require('playwright');"
            "const browser=await chromium.launch({headless:true});"
            "try{console.log(JSON.stringify({browser:'chromium-headless-shell',"
            "version:browser.version(),playwright:require('playwright/package.json').version}));}"
            "finally{await browser.close();}})().catch(error=>{console.error(error);process.exit(1);});"],
            ROOT / 'frontend')
        run('postgres-image', ['docker', 'image', 'inspect', 'postgres:16-alpine'])
        run('postgres-create', ['docker', 'run', '-d', '--name', container, '--label', 'careergap.ui-regression=' + stamp, '-p', f'127.0.0.1:{pg}:5432', '--tmpfs', '/var/lib/postgresql/data', '-e', 'POSTGRES_USER', '-e', 'POSTGRES_PASSWORD', '-e', 'POSTGRES_DB', 'postgres:16-alpine'])
        created = True
        results['postgresReadyAttempts'] = wait_for_postgres(container, database,
            env=env, creation_flags=creation_flags, log_path=out / 'postgres-ready.log')
        results['checks']['postgres-ready'] = 0
        save()
        print('postgres-ready: TCP/SQL query passed', flush=True)
        run('postgres-version', ['docker', 'exec', '-e', 'PGPASSWORD', container,
            'psql', '-h', '127.0.0.1', '-U', 'ui_test', '-d', database, '-X', '-Atc',
            'select version()'], command_env={**env, 'PGPASSWORD': password})
        if not args.skip_frontend:
            for script in ['lint', 'typecheck', 'test', 'build']:
                run('frontend-' + script, [npm, 'run', script], ROOT / 'frontend')
        run('backend-build', ['dotnet', 'build', 'tests/CareerGap.Api.Tests/CareerGap.Api.Tests.csproj', '--artifacts-path', str(build)])
        if not args.skip_backend:
            run('backend-tests', ['dotnet', 'test', 'tests/CareerGap.Api.Tests/CareerGap.Api.Tests.csproj', '--no-build', '--artifacts-path', str(build), '--logger', 'trx;LogFileName=backend.trx', '--results-directory', str(out / 'results')])
        migration = out / 'migrate'
        migration.mkdir()
        (migration / 'Migrate.csproj').write_text(f'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><ProjectReference Include="{(ROOT / "backend/CareerGap.Api/CareerGap.Api.csproj").as_posix()}" /></ItemGroup></Project>', encoding='utf-8')
        (migration / 'Program.cs').write_text('using CareerGap.Api.Data; using Microsoft.EntityFrameworkCore; using Npgsql;\nvar connection = Environment.GetEnvironmentVariable("ConnectionStrings__Database")!; var cs = new NpgsqlConnectionStringBuilder(connection); if (cs.Host != "127.0.0.1" || !cs.Database!.StartsWith("careergap_test_ui_")) throw new Exception("isolated_database_required"); await using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(connection).Options); await db.Database.MigrateAsync(); Console.WriteLine("Explicit migration completed on isolated UI test DB.");', encoding='utf-8')
        run('migration-build', ['dotnet', 'build', str(migration / 'Migrate.csproj'), '--artifacts-path', str(build)])
        run('migration', ['dotnet', str(build / 'bin/Migrate/debug/Migrate.dll')])
        runtime = out / 'runtime'
        shutil.copytree(build / 'bin/CareerGap.Api/debug', runtime, ignore=shutil.ignore_patterns('appsettings.Local.json'))
        dll = runtime / 'CareerGap.Api.dll'
        results['apiAssemblySha256'] = hashlib.sha256(dll.read_bytes()).hexdigest()
        run('seed', ['dotnet', str(dll), '--create-demo-user'], runtime)
        api = start('api', ['dotnet', str(dll), '--urls', api_url], runtime)
        ready(api_url + '/api/health', api)
        vite_config = out / 'vite.config.mjs'
        vite_config.write_text('import {createRequire} from "node:module";import {pathToFileURL} from "node:url";const r=createRequire(' + json.dumps(str(ROOT / 'frontend/package.json')) + ');const {defineConfig}=await import(pathToFileURL(r.resolve("vite")).href);const {default:react}=await import(pathToFileURL(r.resolve("@vitejs/plugin-react")).href);export default defineConfig({root:' + json.dumps(str(ROOT / 'frontend')) + ',plugins:[react()],server:{host:"127.0.0.1",port:' + str(vite_port) + ',strictPort:true,proxy:{"/api":' + json.dumps(api_url) + '}}});', encoding='utf-8')
        vite = start('vite', ['node', str(ROOT / 'frontend/node_modules/vite/bin/vite.js'), '--config', str(vite_config)], ROOT / 'frontend')
        ready(ui_url, vite)
        results['uiUrl'] = ui_url
        save()
        run('e2e', [npm, 'run', 'e2e', '--', *args.tests], ROOT / 'frontend')
        sql = "SELECT json_build_object('non_fake_methods',(SELECT count(*) FROM ai_method_versions WHERE provider <> 'Fake'),'usd_budgets',(SELECT count(*) FROM budget_periods WHERE unit = 'usd'),'jobs',(SELECT json_agg(t) FROM (SELECT job_type,status,count(*) AS count FROM ai_jobs GROUP BY job_type,status) t),'budgets',(SELECT json_agg(t) FROM (SELECT service,unit,spent_amount,reserved_amount FROM budget_periods) t));"
        audit = subprocess.run(['docker', 'exec', container, 'psql', '-U', 'ui_test', '-d', database, '-Atc', sql], capture_output=True, text=True, creationflags=creation_flags)
        (out / 'provider-audit.log').write_text(audit.stdout + audit.stderr, encoding='utf-8')
        if audit.returncode:
            raise RuntimeError('Isolated provider ledger audit failed')
        results['uiDatabaseAudit'] = json.loads(audit.stdout)
        if results['uiDatabaseAudit']['non_fake_methods'] or results['uiDatabaseAudit']['usd_budgets']:
            raise RuntimeError('Unexpected real-provider ledger in isolated UI database')
        results['completed'] = True
    except Exception as error:
        results['failure'] = type(error).__name__
        if created:
            with (out / 'postgres-container.log').open('w', encoding='utf-8') as log:
                try:
                    subprocess.run(['docker', 'logs', container], stdout=log,
                        stderr=subprocess.STDOUT, creationflags=creation_flags, timeout=10)
                except (OSError, subprocess.TimeoutExpired) as diagnostic_error:
                    log.write(type(diagnostic_error).__name__)
        raise
    finally:
        for process, log in reversed(processes):
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
            log.close()
        if created:
            inspection = subprocess.run(['docker', 'inspect', '--format', '{{index .Config.Labels "careergap.ui-regression"}}', container], capture_output=True, text=True, creationflags=creation_flags)
            if inspection.returncode == 0 and inspection.stdout.strip() == stamp:
                results['cleanup'] = subprocess.run(['docker', 'rm', '-f', container], stdout=subprocess.DEVNULL, creationflags=creation_flags).returncode == 0
        save()
        print('Results: ' + str(out / 'result.json'), flush=True)


if __name__ == '__main__':
    main()
