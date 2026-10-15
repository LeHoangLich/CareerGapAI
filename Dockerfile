FROM node:24.16.0-bookworm-slim AS frontend
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci --no-fund
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS backend
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY backend/CareerGap.Api/CareerGap.Api.csproj backend/CareerGap.Api/packages.lock.json backend/CareerGap.Api/
RUN dotnet restore backend/CareerGap.Api/CareerGap.Api.csproj --locked-mode
COPY backend/ backend/
RUN dotnet publish backend/CareerGap.Api/CareerGap.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false
COPY --from=frontend /src/frontend/dist/ /out/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0.12
WORKDIR /app
COPY --from=backend /out/ ./
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
# Production requires private storage, database and DP certificate secrets; migrations run separately.
ENTRYPOINT ["dotnet", "CareerGap.Api.dll"]
