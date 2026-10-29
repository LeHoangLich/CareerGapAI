"""Readiness regressions; no Docker, database or provider is called by these tests."""
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import Mock

spec = importlib.util.spec_from_file_location('frontend_runner', Path(__file__).with_name('test-frontend-regression.py'))
runner = importlib.util.module_from_spec(spec)
spec.loader.exec_module(runner)


class Clock:
    def __init__(self):
        self.now = 0

    def read(self):
        return self.now

    def sleep(self, duration):
        self.now += duration


class PostgresReadinessTests(unittest.TestCase):
    database = 'careergap_test_ui_readiness'

    def probe(self, run_command, log_path, timeout=1):
        clock = Clock()
        return runner.wait_for_postgres('owned-test-container', self.database,
            env={'POSTGRES_PASSWORD': 'synthetic-password'}, creation_flags=0,
            log_path=log_path, timeout=timeout, run_command=run_command,
            sleep=clock.sleep, clock=clock.read)

    def response(self, code=0, output=None, error=''):
        return subprocess.CompletedProcess([], code, self.database + '|1\n' if output is None else output, error)

    def test_probes_tcp_and_target_database_with_password_outside_arguments(self):
        process = Mock(return_value=self.response())
        with tempfile.TemporaryDirectory() as temp:
            log = Path(temp) / 'ready.log'
            self.assertEqual(self.probe(process, log), 1)
            command = process.call_args.args[0]
            self.assertEqual(command[command.index('-h') + 1], '127.0.0.1')
            self.assertEqual(command[command.index('-d') + 1], self.database)
            self.assertIn('SELECT current_database(), 1', command)
            self.assertEqual(process.call_args.kwargs['env']['PGPASSWORD'], 'synthetic-password')
            self.assertNotIn('synthetic-password', ' '.join(command))
            self.assertNotIn('synthetic-password', log.read_text())

    def test_retries_startup_connections_until_final_server_query_succeeds(self):
        process = Mock(side_effect=[self.response(2, '', 'connection refused\n'), self.response(2, '', 'starting up\n'), self.response()])
        with tempfile.TemporaryDirectory() as temp:
            self.assertEqual(self.probe(process, Path(temp) / 'ready.log'), 3)

    def test_success_exit_without_target_database_result_does_not_mark_ready(self):
        process = Mock(side_effect=[self.response(output='postgres|1\n'), self.response()])
        with tempfile.TemporaryDirectory() as temp:
            self.assertEqual(self.probe(process, Path(temp) / 'ready.log'), 2)

    def test_query_failure_is_not_ready_even_if_server_accepts_connection(self):
        process = Mock(side_effect=[self.response(2, '', 'database does not exist\n'), self.response()])
        with tempfile.TemporaryDirectory() as temp:
            self.assertEqual(self.probe(process, Path(temp) / 'ready.log'), 2)

    def test_individual_probe_timeout_is_retried(self):
        process = Mock(side_effect=[subprocess.TimeoutExpired('probe', 5), self.response()])
        with tempfile.TemporaryDirectory() as temp:
            log = Path(temp) / 'ready.log'
            self.assertEqual(self.probe(process, log), 2)
            self.assertIn('probe_timeout', log.read_text())

    def test_readiness_deadline_fails_and_keeps_diagnostic_log(self):
        process = Mock(return_value=self.response(2, '', 'authentication failed\n'))
        with tempfile.TemporaryDirectory() as temp:
            log = Path(temp) / 'ready.log'
            with self.assertRaisesRegex(RuntimeError, 'TCP/SQL readiness timed out'):
                self.probe(process, log)
            self.assertGreater(process.call_count, 1)
            self.assertIn('authentication failed', log.read_text())


if __name__ == '__main__':
    unittest.main()
