# Multi-stage build: the SDK image builds and publishes, the small ASP.NET runtime image runs the app.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached when only source files change.
COPY ClinicBooking.slnx ./
COPY src/ClinicBooking.Domain/*.csproj src/ClinicBooking.Domain/
COPY src/ClinicBooking.Contracts/*.csproj src/ClinicBooking.Contracts/
COPY src/ClinicBooking.Application/*.csproj src/ClinicBooking.Application/
COPY src/ClinicBooking.Infrastructure/*.csproj src/ClinicBooking.Infrastructure/
COPY src/ClinicBooking.Client/*.csproj src/ClinicBooking.Client/
COPY src/ClinicBooking.Api/*.csproj src/ClinicBooking.Api/
RUN dotnet restore src/ClinicBooking.Api/ClinicBooking.Api.csproj

COPY src/ src/
RUN dotnet publish src/ClinicBooking.Api/ClinicBooking.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "ClinicBooking.Api.dll"]
