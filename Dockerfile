FROM mcr.microsoft.com/dotnet/sdk:10.0.101

WORKDIR /app

COPY . .

RUN chmod +x run_tests.sh parse_results.sh || true

RUN dotnet restore Ticketing.sln && \
    dotnet build Ticketing.sln --no-restore --configuration Release