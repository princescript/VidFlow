# ---------- Stage 1: Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["server.csproj", "./"]
RUN dotnet restore "server.csproj"

COPY . .
RUN dotnet publish "server.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---------- Stage 2: Pre-download ffmpeg ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS ffmpeg-downloader
WORKDIR /ffmpeg-dl

RUN dotnet new console -n FfDownloader -o . \
 && dotnet add package Xabe.FFmpeg.Downloader

RUN cat > Program.cs <<'EOF'
using Xabe.FFmpeg.Downloader;
using Xabe.FFmpeg;

var target = "/ffmpeg";
Directory.CreateDirectory(target);
await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, target);
Console.WriteLine("ffmpeg downloaded to " + target);
EOF

RUN dotnet run --project . --configuration Release

# ---------- Stage 3: Pre-download Whisper model ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS whisper-model-downloader
WORKDIR /model

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates \
 && rm -rf /var/lib/apt/lists/*

# Download the tiny model (~75 MB)
RUN curl -L -o ggml-tiny.bin \
    https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin

# Fail the build if the download didn't work (guards against silent 404s / HTML error pages)
RUN test $(stat -c%s ggml-tiny.bin) -gt 10000000

# ---------- Stage 4: Runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Native deps for Whisper.net
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
    libstdc++6 \
    zlib1g \
    libgomp1 \
 && rm -rf /var/lib/apt/lists/*

# ffmpeg binaries
COPY --from=ffmpeg-downloader /ffmpeg /app/ffmpeg

# Whisper model — baked in, exactly where WhisperService looks
COPY --from=whisper-model-downloader /model/ggml-tiny.bin /app/models/ggml-tiny.bin

# App
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Docker
EXPOSE 8080

ENTRYPOINT ["dotnet", "server.dll"]