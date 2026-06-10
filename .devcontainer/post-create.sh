#!/usr/bin/env bash
set -euo pipefail

# Playwright setup for browser-based UI testing (Dependinator.E2E.Tests) and
# agent browser checks (Claude Code via playwright-cli).
#
# PLAYWRIGHT_VERSION must match the Microsoft.Playwright version in
# Directory.Packages.props, so the .NET tests and the CLI share the same
# browser builds in ~/.cache/ms-playwright.
PLAYWRIGHT_VERSION="1.60.0"

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
