# Dockerfile dùng chung cho mọi service .NET (BE-08). Build context = backend/.
#   PROJECT  đường dẫn csproj tính từ backend/, ví dụ Services/Identity/Identity.Api/Identity.Api.csproj
#   APP_DLL  tên file dll khởi động, ví dụ Identity.Api.dll
# Ví dụ: docker build -f deploy/docker/dotnet.Dockerfile --build-arg PROJECT=... --build-arg APP_DLL=... backend

# 1. Chỉ giữ csproj + Directory.Build.props để lớp restore được cache khi chỉ sửa code.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS projects
WORKDIR /src
COPY . .
RUN find . -type f ! -name '*.csproj' ! -name 'Directory.Build.props' -delete

# 2. Restore rồi publish đúng project được chọn.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG PROJECT
WORKDIR /src
COPY --from=projects /src .
RUN dotnet restore "$PROJECT"
COPY . .
RUN dotnet publish "$PROJECT" -c Release -o /app --no-restore /p:UseAppHost=false

# 3. Runtime: chỉ aspnet + curl cho healthcheck.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ARG APP_DLL
ENV APP_DLL=$APP_DLL \
    ASPNETCORE_URLS=http://+:8080
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
