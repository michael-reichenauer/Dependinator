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

# The net11.0 projects need the .NET 11 SDK (global.json). It is installed beside the image's SDK 10,
# whose runtime the net10.0 Api and Shared, the csharpier tool and CSharpier.MsBuild still need.
RUN wget -qO- https://dot.net/v1/dotnet-install.sh \
    | bash -s -- --channel 11.0 --install-dir /usr/share/dotnet
