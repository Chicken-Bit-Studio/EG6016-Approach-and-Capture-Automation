"""
Master test runner for Unity-Python ML pipeline.

Executes all unit and integration tests with custom formatted output.
Uses pytest under the hood but provides custom reporting for better readability.
"""

import sys
import os
from pathlib import Path
import time

# Add project root to path
PROJECT_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROJECT_ROOT))

import pytest
from configs.config import Colors


class TestResult:
    """Container for test result data."""
    def __init__(self, name: str, passed: bool, duration: float, error: str = None):
        self.name = name
        self.passed = passed
        self.duration = duration
        self.error = error


class CustomTestRunner:
    """
    Custom test runner with formatted output.
    
    Provides clean, readable output matching project's visual style
    while leveraging pytest's test discovery and execution.
    """
    
    def __init__(self):
        self.results = {
            'unit': [],
            'integration': []
        }
    
    def run_tests(self) -> int:
        """
        Runs all tests and displays formatted results.
        
        :return: 0 if all tests passed, 1 if any failed
        """
        print(f"\n{Colors.INFO}{'='*70}{Colors.RESET}")
        print(f"{Colors.INFO}Unity-Python ML Pipeline - Test Suite{Colors.RESET}")
        print(f"{Colors.INFO}{'='*70}{Colors.RESET}\n")
        
        # Run unit tests
        print(f"{Colors.INFO}{'='*70}{Colors.RESET}")
        print(f"{Colors.INFO}UNIT TESTS{Colors.RESET}")
        print(f"{Colors.INFO}{'='*70}{Colors.RESET}")
        
        unit_passed, unit_total = self._run_test_category('unit')
        
        # Run integration tests
        print(f"\n{Colors.INFO}{'='*70}{Colors.RESET}")
        print(f"{Colors.INFO}INTEGRATION TESTS{Colors.RESET}")
        print(f"{Colors.INFO}{'='*70}{Colors.RESET}")
        
        integration_passed, integration_total = self._run_test_category('integration')
        
        # Display summary
        self._print_summary(unit_passed, unit_total, integration_passed, integration_total)
        
        # Return exit code
        total_passed = unit_passed + integration_passed
        total_tests = unit_total + integration_total
        return 0 if total_passed == total_tests else 1
    
    def _run_test_category(self, category: str) -> tuple:
        """
        Runs tests in specified category directory.
        
        :param category: 'unit' or 'integration'
        :return: Tuple of (passed_count, total_count)
        """
        test_dir = PROJECT_ROOT / 'tests' / category
        
        if not test_dir.exists():
            print(f"{Colors.WARNING}No {category} tests found{Colors.RESET}\n")
            return 0, 0
        
        # Run pytest with custom reporting
        args = [
            str(test_dir),
            '-v',
            '--tb=short',
            '--color=no'  # We handle colors ourselves
        ]
        
        # Collect test results
        class ResultCollector:
            def __init__(self):
                self.results = []
            
            def pytest_runtest_logreport(self, report):
                if report.when == 'call':
                    self.results.append({
                        'name': report.nodeid,
                        'passed': report.passed,
                        'duration': report.duration,
                        'error': str(report.longrepr) if report.failed else None
                    })
        
        collector = ResultCollector()
        pytest.main(args, plugins=[collector])
        
        # Format and display results
        passed_count = 0
        for result in collector.results:
            name = self._format_test_name(result['name'])
            duration = result['duration']
            
            if result['passed']:
                print(f"{Colors.SUCCESS}[✓]{Colors.RESET} {name:<50} ({duration:.2f}s)")
                passed_count += 1
            else:
                print(f"{Colors.ERROR}[✗]{Colors.RESET} {name:<50} ({duration:.2f}s)")
                if result['error']:
                    # Print first line of error
                    error_preview = result['error'].split('\n')[0][:60]
                    print(f"    {Colors.WARNING}{error_preview}...{Colors.RESET}")
        
        if not collector.results:
            print(f"{Colors.WARNING}No tests found in {category}/{Colors.RESET}")
        
        print()  # Blank line after category
        return passed_count, len(collector.results)
    
    def _format_test_name(self, nodeid: str) -> str:
        """
        Formats pytest node ID into readable test name.
        
        :param nodeid: pytest node ID (e.g., 'tests/unit/test_socket.py::test_connection')
        :return: Formatted name (e.g., 'Socket Client - Connection')
        """
        # Extract filename and test name
        parts = nodeid.split('::')
        if len(parts) < 2:
            return nodeid
        
        filename = Path(parts[0]).stem.replace('test_', '').replace('_', ' ').title()
        testname = parts[1].replace('test_', '').replace('_', ' ').title()
        
        return f"{filename} - {testname}"
    
    def _print_summary(self, unit_passed: int, unit_total: int,
                       integration_passed: int, integration_total: int) -> None:
        """Prints final test summary."""
        print(f"{Colors.INFO}{'='*70}{Colors.RESET}")
        print(f"{Colors.INFO}SUMMARY{Colors.RESET}")
        print(f"{Colors.INFO}{'='*70}{Colors.RESET}")
        
        # Unit tests summary
        unit_pct = (unit_passed / unit_total * 100) if unit_total > 0 else 0
        unit_color = Colors.SUCCESS if unit_passed == unit_total else Colors.ERROR
        print(f"Unit Tests: {unit_color}{unit_passed}/{unit_total} passed ({unit_pct:.1f}%){Colors.RESET}")
        
        # Integration tests summary
        int_pct = (integration_passed / integration_total * 100) if integration_total > 0 else 0
        int_color = Colors.SUCCESS if integration_passed == integration_total else Colors.ERROR
        print(f"Integration Tests: {int_color}{integration_passed}/{integration_total} passed ({int_pct:.1f}%){Colors.RESET}")
        
        # Overall
        total_passed = unit_passed + integration_passed
        total_tests = unit_total + integration_total
        overall_pct = (total_passed / total_tests * 100) if total_tests > 0 else 0
        overall_color = Colors.SUCCESS if total_passed == total_tests else Colors.ERROR
        
        print(f"\n{overall_color}Overall: {total_passed}/{total_tests} passed ({overall_pct:.1f}%){Colors.RESET}")
        print(f"{Colors.INFO}{'='*70}{Colors.RESET}\n")


def main():
    """Main entry point for test runner."""
    runner = CustomTestRunner()
    exit_code = runner.run_tests()
    
    sys.exit(exit_code)


if __name__ == "__main__":
    main()
