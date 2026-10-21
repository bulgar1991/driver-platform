# API image. Build from the backend folder: docker build -t magnus-api backend
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DriverPlatform.slnx ./
COPY src/DriverPlatform.Domain/DriverPlatform.Domain.csproj src/DriverPlatform.Domain/
COPY src/DriverPlatform.Application/DriverPlatform.Application.csproj src/DriverPlatform.Application/
COPY src/DriverPlatform.Infrastructure/DriverPlatform.Infrastructure.csproj src/DriverPlatform.Infrastructure/
COPY src/DriverPlatform.Api/DriverPlatform.Api.csproj src/DriverPlatform.Api/
RUN dotnet restore src/DriverPlatform.Api/DriverPlatform.Api.csproj
COPY src/ src/
RUN dotnet publish src/DriverPlatform.Api/DriverPlatform.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# tzdata: pickup times are converted with Europe/Rome. Fonts and fontconfig: the PDF invoices (QuestPDF).
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata libfontconfig1 fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0
EXPOSE 8080
# The image's non-root user.
USER $APP_UID
ENTRYPOINT ["dotnet", "DriverPlatform.Api.dll"]
