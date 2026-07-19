#!/usr/bin/env bash
# Runs the test suite with code coverage and builds a browsable HTML report.
# Free tooling only, coverlet (already referenced by the test project) plus ReportGenerator.
# The setup follows the walkthrough in Microsoft Learn, "Use code coverage for unit testing",
# https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage
#
#   ./coverage.sh
#
# The report lands in coverage/index.html and is opened when possible. Line-level highlighting,
# covered lines green and missed lines red, is on each file's page in the report.

set -euo pipefail
cd "$(dirname "$0")"

command -v dotnet >/dev/null || { echo "FAIL  dotnet is not on PATH"; exit 1; }

if ! command -v reportgenerator >/dev/null; then
    echo "ReportGenerator is not installed, installing it as a dotnet global tool ..."
    dotnet tool install -g dotnet-reportgenerator-globaltool
    echo "If the command is still not found, open a new terminal so PATH picks up ~/.dotnet/tools."
fi

echo "Running tests with coverage ..."
rm -rf Gaia.Core.Tests/TestResults
dotnet test --collect:"XPlat Code Coverage" --nologo

echo "Building the HTML report ..."
reportgenerator \
    -reports:"Gaia.Core.Tests/TestResults/*/coverage.cobertura.xml" \
    -targetdir:coverage \
    -reporttypes:"Html;TextSummary"

echo
cat coverage/Summary.txt
echo
echo "Full report, coverage/index.html"
# Open the report where the platform allows it. Git Bash on Windows has start.
if command -v start >/dev/null 2>&1; then start coverage/index.html
elif command -v xdg-open >/dev/null 2>&1; then xdg-open coverage/index.html
elif command -v open >/dev/null 2>&1; then open coverage/index.html
fi
