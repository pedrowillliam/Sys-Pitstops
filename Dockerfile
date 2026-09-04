# Serviço único: o ASP.NET serve a API e a SPA já compilada (docs/decisions.md,
# D-26). O contexto de build é a raiz do repositório, porque a imagem precisa
# de backend/ e frontend/.

# ---------------------------------------------------------------------------
# 1. PWA
# ---------------------------------------------------------------------------
FROM node:22-alpine AS frontend

WORKDIR /src

# package.json e lock primeiro: enquanto as dependências não mudarem, o cache
# do Docker pula o npm ci inteiro.
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build

# ---------------------------------------------------------------------------
# 2. API
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend

WORKDIR /src

COPY backend/SysPitstops.slnx ./
COPY backend/src/SysPitstops.Api/SysPitstops.Api.csproj src/SysPitstops.Api/
COPY backend/tests/SysPitstops.Tests/SysPitstops.Tests.csproj tests/SysPitstops.Tests/
RUN dotnet restore

COPY backend/ ./
RUN dotnet publish src/SysPitstops.Api/SysPitstops.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app

# ---------------------------------------------------------------------------
# 3. Imagem final
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

COPY --from=backend /app ./
# UseStaticFiles serve a partir de wwwroot; é aqui que a SPA vira "a mesma
# origem" que o cookie SameSite=Lax da D-20 exige.
COPY --from=frontend /src/dist ./wwwroot

# O Render injeta PORT em tempo de execução e o Program.cs lê essa variável.
# 8080 é só o padrão para rodar a imagem na mão.
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Não roda como root: se algum dia um upload escapar da validação, o processo
# não tem permissão para reescrever a própria aplicação.
USER $APP_UID

ENTRYPOINT ["dotnet", "SysPitstops.Api.dll"]
