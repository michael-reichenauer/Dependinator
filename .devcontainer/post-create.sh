#!/usr/bin/env bash
set -euo pipefail

# Playwright setup for browser-based UI testing (Dependinator.E2E.Tests) and
# agent browser checks (Claude Code via playwright-cli).
#
# PLAYWRIGHT_VERSION is the Microsoft.Playwright.Xunit version in
# Directory.Packages.props, so the .NET tests and the CLI share the same
# browser builds in ~/.cache/ms-playwright (scripts/cloud-setup reads it the same way).
PLAYWRIGHT_VERSION="$(sed -n 's/.*"Microsoft.Playwright.Xunit" Version="\([^"]*\)".*/\1/p' \
    "$(dirname "$0")/../Directory.Packages.props")"
if [[ -z "$PLAYWRIGHT_VERSION" ]]; then
    echo "Could not read the Microsoft.Playwright.Xunit version from Directory.Packages.props." >&2
    exit 1
fi

echo "Installing Playwright CLI (agent browser automation)..."
npm install -g @playwright/cli@latest

echo "Installing Playwright browser OS dependencies..."
# node/npx live under the vscode user's nvm dir, so preserve PATH for sudo
sudo env "PATH=${PATH}" npx -y "playwright@${PLAYWRIGHT_VERSION}" install-deps chromium firefox webkit

echo "Installing Playwright browsers (chromium, firefox, webkit)..."
npx -y "playwright@${PLAYWRIGHT_VERSION}" install chromium firefox webkit

# Refresh the playwright-cli agent skill (committed in .claude/skills/playwright-cli)
# and download the browser build matching the installed CLI version.
(cd /workspaces/Dependinator && playwright-cli install --skills) || true

echo "Playwright setup done."
