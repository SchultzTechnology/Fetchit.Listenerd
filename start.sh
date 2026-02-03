#!/bin/bash
set -e

echo "=========================================="
echo "  Fetchit Listenerd Service"
echo "=========================================="
echo ""

#--------------------------------------------
# Variables
#--------------------------------------------
APP_ROOT="/app"
SUPERVISOR_CONF="/etc/supervisor/conf.d/fetchit.conf"
PUBLISH_PATH=${APP_ROOT}/fetchit
GITHUB_REPO="SchultzTechnology/Fetchit.Listenerd"
DOWNLOAD_URL="https://github.com/${GITHUB_REPO}/releases/latest/download/fetchit-linux-arm64.zip"

#--------------------------------------------
# Detect OS and Distribution
#--------------------------------------------
detect_os() {
    if [ -f /etc/os-release ]; then
        . /etc/os-release
        OS=$ID
        OS_VERSION=$VERSION_ID
        OS_PRETTY=$PRETTY_NAME
    elif [ -f /etc/debian_version ]; then
        OS="debian"
        OS_VERSION=$(cat /etc/debian_version)
        OS_PRETTY="Debian $OS_VERSION"
    elif [ -f /etc/redhat-release ]; then
        OS="rhel"
        OS_VERSION=$(cat /etc/redhat-release)
        OS_PRETTY=$OS_VERSION
    else
        OS="unknown"
        OS_PRETTY="Unknown OS"
    fi
    
    echo "Detected OS: $OS_PRETTY"
}

#--------------------------------------------
# Ensure script is run as root
#--------------------------------------------
if [ "$EUID" -ne 0 ]; then
  echo "❌ Please run this script as root (use sudo)"
  exit 1
fi

detect_os

#--------------------------------------------
# Install system dependencies based on OS
#--------------------------------------------
echo "Installing system dependencies..."

case $OS in
    ubuntu|debian|raspbian)
        export DEBIAN_FRONTEND=noninteractive
        apt-get update
        
        # Determine correct libpcap package name
        LIBPCAP_PKG="libpcap0.8"
        if apt-cache show libpcap0.8t64 &>/dev/null; then
            LIBPCAP_PKG="libpcap0.8t64"
        fi
        
        # Pre-configure iptables-persistent to avoid interactive prompts
        echo iptables-persistent iptables-persistent/autosave_v4 boolean true | debconf-set-selections
        echo iptables-persistent iptables-persistent/autosave_v6 boolean true | debconf-set-selections
        
        apt-get install -y \
          wget \
          curl \
          $LIBPCAP_PKG \
          supervisor \
          iptables-persistent
        ;;
        
    fedora|rhel|centos|rocky|almalinux)
        if command -v dnf &> /dev/null; then
            PKG_MGR="dnf"
        else
            PKG_MGR="yum"
        fi
        
        $PKG_MGR install -y \
          wget \
          curl \
          libpcap \
          supervisor \
          iptables-services
        
        # Enable iptables service
        systemctl enable iptables
        ;;
        
    arch|manjaro)
        pacman -Sy --noconfirm \
          wget \
          curl \
          libpcap \
          supervisor \
          iptables
        ;;
        
    alpine)
        apk update
        apk add --no-cache \
          wget \
          curl \
          libpcap \
          supervisor \
          iptables
        ;;
        
    *)
        echo "⚠️  Unsupported OS: $OS_PRETTY"
        echo "Please install the following packages manually:"
        echo "  - wget, curl"
        echo "  - libpcap (or libpcap0.8)"
        echo "  - supervisor"
        echo "  - iptables"
        read -p "Continue anyway? (y/N) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            exit 1
        fi
        ;;
esac

echo "Downloading latest release from GitHub..."
mkdir -p ${PUBLISH_PATH}

# Download release zip
if ! wget -q --show-progress ${DOWNLOAD_URL} -O /tmp/fetchit-linux-arm64.zip; then
    echo "❌ Failed to download release from GitHub"
    echo "URL: ${DOWNLOAD_URL}"
    echo ""
    echo "Please ensure:"
    echo "  1. A release has been published on GitHub"
    echo "  2. The release contains fetchit-linux-arm64.zip"
    echo "  3. Your device has internet connectivity"
    exit 1
fi

echo "Extracting binaries..."
unzip -q -o /tmp/fetchit-linux-arm64.zip -d ${PUBLISH_PATH}

echo "Setting executable permissions..."
chmod +x ${PUBLISH_PATH}/listenerd/Fetchit.Listenerd
chmod +x ${PUBLISH_PATH}/webpage/Fetchit.WebPage

# Clean up
rm -f /tmp/fetchit-linux-arm64.zip

# Create data directory
mkdir -p ${APP_ROOT}/data

echo "Configuring firewall..."
case $OS in
    ubuntu|debian|raspbian)
        iptables -A INPUT -p tcp --dport 8080 -j ACCEPT
        if command -v netfilter-persistent &> /dev/null; then
            netfilter-persistent save
        else
            echo "⚠️  netfilter-persistent not available, firewall rules are temporary"
        fi
        ;;
        
    fedora|rhel|centos|rocky|almalinux)
        if command -v firewall-cmd &> /dev/null; then
            firewall-cmd --permanent --add-port=8080/tcp
            firewall-cmd --reload
        else
            iptables -A INPUT -p tcp --dport 8080 -j ACCEPT
            service iptables save
        fi
        ;;
        
    *)
        iptables -A INPUT -p tcp --dport 8080 -j ACCEPT
        echo "⚠️  Firewall configuration may need manual persistence for your OS"
        ;;
esac

echo "Creating Supervisor configuration..."

cat > ${SUPERVISOR_CONF} <<EOF
[program:fetchit-listenerd]
command=${PUBLISH_PATH}/listenerd/Fetchit.Listenerd
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
command=${PUBLISH_PATH}/webpage/Fetchit.WebPage
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
