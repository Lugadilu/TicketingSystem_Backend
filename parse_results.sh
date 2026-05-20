#!/bin/bash

if [ ! -f /app/test_results.trx ]; then
    echo "ERROR: No test results found"
    exit 1
fi

passed=$(grep -o 'outcome="Passed"' /app/test_results.trx | wc -l)
failed=$(grep -o 'outcome="Failed"' /app/test_results.trx | wc -l)
total=$((passed + failed))

echo "Tests: $total total, $passed passed, $failed failed"

if [ "$failed" -gt 0 ]; then
    exit 1
else
    exit 0
fi