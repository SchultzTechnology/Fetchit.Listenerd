# Combined Dockerfile for both WebPage and Listenerd
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["Fetchit.Listenerd/Fetchit.Listenerd.csproj", "Fetchit.Listenerd/"]
COPY ["Fetchit.WebPage/Fetchit.WebPage.csproj", "Fetchit.WebPage/"]
RUN dotnet restore "Fetchit.Listenerd/Fetchit.Listenerd.csproj"
RUN dotnet restore "Fetchit.WebPage/Fetchit.WebPage.csproj"

# Copy everything else and build
COPY . .

# Build Listenerd
WORKDIR "/src/Fetchit.Listenerd"
RUN dotnet build "Fetchit.Listenerd.csproj" -c Release -o /app/build/listenerd

# Build WebPage
WORKDIR "/src/Fetchit.WebPage"
RUN dotnet build "Fetchit.WebPage.csproj" -c Release -o /app/build/webpage

# Publish stage
FROM build AS publish
WORKDIR "/src/Fetchit.Listenerd"
RUN dotnet publish "Fetchit.Listenerd.csproj" -c Release -o /app/publish/listenerd /p:UseAppHost=false

WORKDIR "/src/Fetchit.WebPage"
RUN dotnet publish "Fetchit.WebPage.csproj" -c Release -o /app/publish/webpage /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Install libpcap for SharpPcap and supervisor for process management
RUN apt-get update && apt-get install -y \
    libpcap0.8 \
    supervisor \
    && rm -rf /var/lib/apt/lists/*

# Copy published applications
COPY --from=publish /app/publish/listenerd ./listenerd
COPY --from=publish /app/publish/webpage ./webpage

# Create directory for SQLite database
RUN mkdir -p /app/data

# Create supervisor configuration with Unix socket
RUN mkdir -p /var/log/supervisor /var/run
COPY <<EOF /etc/supervisor/conf.d/supervisord.conf
[unix_http_server]
file=/var/run/supervisor.sock
chmod=0700

[supervisord]
nodaemon=true
user=root
logfile=/var/log/supervisor/supervisord.log
pidfile=/var/run/supervisord.pid

[rpcinterface:supervisor]
supervisor.rpcinterface_factory = supervisor.rpcinterface:make_main_rpcinterface

[supervisorctl]
serverurl=unix:///var/run/supervisor.sock

[program:fetchit-listenerd]
command=dotnet /app/listenerd/Fetchit.Listenerd.dll
directory=/app/listenerd
autostart=true
autorestart=true
stderr_logfile=/var/log/supervisor/listenerd.err.log
stdout_logfile=/var/log/supervisor/listenerd.out.log
stdout_logfile_maxbytes=10MB
stderr_logfile_maxbytes=10MB
priority=1

[program:fetchit-webpage]
command=dotnet /app/webpage/Fetchit.WebPage.dll
directory=/app/webpage
autostart=true
autorestart=true
stderr_logfile=/var/log/supervisor/webpage.err.log
stdout_logfile=/var/log/supervisor/webpage.out.log
stdout_logfile_maxbytes=10MB
stderr_logfile_maxbytes=10MB
priority=2
EOF

EXPOSE 8080
EXPOSE 5060/udp

# Run supervisor to manage both processes
CMD ["/usr/bin/supervisord", "-c", "/etc/supervisor/conf.d/supervisord.conf"]
