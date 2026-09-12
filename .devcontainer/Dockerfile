FROM mcr.microsoft.com/dotnet/sdk:10.0

RUN apt-get update \
    && apt-get install -y wget gnupg ca-certificates apt-transport-https libicu-dev \
    && wget -q https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y azure-functions-core-tools-4 \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# The C# 15 union patterns (see global.json) need the .NET 11 SDK. It is installed beside the image's
# SDK 10, whose runtime the net10.0 projects, the csharpier tool and CSharpier.MsBuild still need.
RUN wget -qO- https://dot.net/v1/dotnet-install.sh \
    | bash -s -- --channel 11.0 --quality preview --install-dir /usr/share/dotnet
