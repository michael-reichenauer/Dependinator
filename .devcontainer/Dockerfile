FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
    && apt-get install -y wget gnupg ca-certificates apt-transport-https libicu-dev sudo \
    && wget -q https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y azure-functions-core-tools-4 \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Create vscode user with sudo access
RUN groupadd --gid 1001 vscode \
    && useradd --uid 1001 --gid 1001 -m vscode \
    && echo "vscode ALL=(ALL) NOPASSWD:ALL" >> /etc/sudoers

USER vscode