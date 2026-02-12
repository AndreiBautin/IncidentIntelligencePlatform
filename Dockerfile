FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/IncidentBrain.API/IncidentBrain.API.csproj", "IncidentBrain.API/"]
COPY ["src/IncidentBrain.Infrastructure/IncidentBrain.Infrastructure.csproj", "IncidentBrain.Infrastructure/"]
COPY ["src/IncidentBrain.Core/IncidentBrain.Core.csproj", "IncidentBrain.Core/"]
RUN dotnet restore IncidentBrain.API/IncidentBrain.API.csproj
COPY src/ .
RUN dotnet publish IncidentBrain.API/IncidentBrain.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
RUN groupadd -r appgroup && useradd -r -g appgroup -m -d /app appuser
COPY --from=build /app/publish .
RUN mkdir -p /data && chown -R appuser:appgroup /app /data
USER appuser
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "IncidentBrain.API.dll"]
