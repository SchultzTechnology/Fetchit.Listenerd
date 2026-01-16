#!/bin/bash
set -e

echo "=========================================="
echo "  Fetchit Listenerd Service"
echo "=========================================="
echo ""

#--------------------------------------------
# Variables
#--------------------------------------------
DOTNET_VERSION="9.0"
DOTNET_INSTALL_DIR="/usr/share/dotnet"
APP_ROOT="/app"
SUPERVISOR_CONF="/etc/supervisor/conf.d/fetchit.conf"
PUBLISH_PATH=${APP_ROOT}/fetchit

#--------------------------------------------
# Ensure script is run as root
#--------------------------------------------
if [ "$EUID" -ne 0 ]; then
  echo "❌ Please run this script as root (use sudo)"
  exit 1
fi

echo "Installing system dependencies..."
apt-get update
apt-get install -y \
  wget \
  curl \
  libpcap0.8t64 \
  supervisor \
  iptables-persistent

# Check if dotnet exists and is working properly
DOTNET_WORKS=false
if command -v dotnet &> /dev/null; then
    if dotnet --version &> /dev/null; then
        DOTNET_WORKS=true
        echo ".NET SDK is already installed and working."
    else
        echo "⚠️ .NET installation is corrupted. Reinstalling..."
        rm -rf ${DOTNET_INSTALL_DIR}
        rm -f /usr/bin/dotnet
    fi
fi

if [ "$DOTNET_WORKS" = false ]; then
    echo "Installing .NET SDK ${DOTNET_VERSION} (LTS)..."
    wget https://dot.net/v1/dotnet-install.sh -O /tmp/dotnet-install.sh
    chmod +x /tmp/dotnet-install.sh
    /tmp/dotnet-install.sh \
        --channel ${DOTNET_VERSION} \
        --install-dir ${DOTNET_INSTALL_DIR}
    ln -sf ${DOTNET_INSTALL_DIR}/dotnet /usr/bin/dotnet
    
    # Verify installation
    if ! dotnet --version &> /dev/null; then
        echo "❌ .NET installation failed!"
        exit 1
    fi
fi

dotnet --version
echo ""

echo "Restoring .NET projects..."
dotnet restore "Fetchit.Listenerd/Fetchit.Listenerd.csproj"
dotnet restore "Fetchit.WebPage/Fetchit.WebPage.csproj"

echo "Building projects..."
dotnet build "Fetchit.Listenerd/Fetchit.Listenerd.csproj" -c Release
dotnet build "Fetchit.WebPage/Fetchit.WebPage.csproj" -c Release

echo "Publishing projects..."
mkdir -p ${PUBLISH_PATH}

dotnet publish "Fetchit.Listenerd/Fetchit.Listenerd.csproj" \
  -c Release \
  -o ${PUBLISH_PATH}/listenerd \
  /p:UseAppHost=false

dotnet publish "Fetchit.WebPage/Fetchit.WebPage.csproj" \
  -c Release \
  -o ${PUBLISH_PATH}/webpage \
  /p:UseAppHost=false

mkdir -p ${APP_ROOT}/data

echo "Configuring firewall..."
iptables -A INPUT -p tcp --dport 8080 -j ACCEPT
netfilter-persistent save

echo "Creating Supervisor configuration..."

cat > ${SUPERVISOR_CONF} <<EOF
[program:fetchit-listenerd]
command=/usr/bin/dotnet ${PUBLISH_PATH}/listenerd/Fetchit.Listenerd.dll
directory=${PUBLISH_PATH}/listenerd
autostart=true
autorestart=true
startretries=3
stderr_logfile=/var/log/supervisor/listenerd.err.log
stderr_logfile_maxbytes=10MB
stderr_logfile_backups=3
stdout_logfile=/var/log/supervisor/listenerd.out.log
stdout_logfile_maxbytes=10MB
stdout_logfile_backups=3
priority=1

[program:fetchit-webpage]
command=/usr/bin/dotnet ${PUBLISH_PATH}/webpage/Fetchit.WebPage.dll
directory=${PUBLISH_PATH}/webpage
autostart=true
autorestart=true
startretries=3
environment=ASPNETCORE_URLS="http://0.0.0.0:8080",ASPNETCORE_ENVIRONMENT="Production"
stderr_logfile=/var/log/supervisor/webpage.err.log
stderr_logfile_maxbytes=10MB
stderr_logfile_backups=3
stdout_logfile=/var/log/supervisor/webpage.out.log
stdout_logfile_maxbytes=10MB
stdout_logfile_backups=3
priority=2
EOF

echo "Starting Supervisor..."
systemctl enable supervisor
systemctl restart supervisor

sleep 2
supervisorctl reread
supervisorctl update

echo ""
echo "=========================================="
echo "✅ Services started successfully!"
echo ""
echo "Web Application : http://localhost:8080"
echo "Listener Service: Running in background"
echo ""
echo "Useful commands:"
echo "  Check status : sudo supervisorctl status"
echo "  View logs   : sudo tail -f /var/log/supervisor/*.log"
echo ""
echo "Database location:"
echo "  ${APP_ROOT}/data/mqttconfig.db"
echo "=========================================="
