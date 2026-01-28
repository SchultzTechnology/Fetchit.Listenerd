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
        apt-get update
        
        # Determine correct libpcap package name
        LIBPCAP_PKG="libpcap0.8"
        if apt-cache show libpcap0.8t64 &>/dev/null; then
            LIBPCAP_PKG="libpcap0.8t64"
        fi
        
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
    
    # Completely clean up any previous installation attempts
    echo "Cleaning up old installation files..."
    rm -rf /var/tmp/dotnet-* /tmp/dotnet-* 2>/dev/null || true
    rm -rf ${DOTNET_INSTALL_DIR} 2>/dev/null || true
    rm -rf /root/.dotnet 2>/dev/null || true
    rm -f /usr/bin/dotnet 2>/dev/null || true
    
    # Install to root home directory first (more reliable)
    TEMP_INSTALL_DIR="/root/.dotnet"
    mkdir -p ${TEMP_INSTALL_DIR}
    
    # Use /var/tmp which has more space
    export TMPDIR=/var/tmp
    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    
    # Download installer
    echo "Downloading .NET installer..."
    wget -q --show-progress https://dot.net/v1/dotnet-install.sh -O /var/tmp/dotnet-install.sh
    chmod +x /var/tmp/dotnet-install.sh
    
    # Run installation to temp location with --force to overwrite any partial installations
    echo "Installing .NET SDK (this may take a few minutes)..."
    /var/tmp/dotnet-install.sh \
        --channel ${DOTNET_VERSION} \
        --install-dir ${TEMP_INSTALL_DIR} \
        --no-path \
        --skip-non-versioned-files \
        --verbose || {
            echo "❌ Installation failed. Checking available space..."
            df -h /var/tmp /root
            exit 1
        }
    
    # Move to final location
    echo "Moving to system location..."
    mkdir -p $(dirname ${DOTNET_INSTALL_DIR})
    mv ${TEMP_INSTALL_DIR} ${DOTNET_INSTALL_DIR}
    
    # Create symlink
    ln -sf ${DOTNET_INSTALL_DIR}/dotnet /usr/bin/dotnet
    
    # Clean up installation files
    echo "Cleaning up temporary files..."
    rm -rf /var/tmp/dotnet-* /tmp/dotnet-*
fi

# Final verification
echo "Verifying .NET installation..."
if ! dotnet --version &> /dev/null; then
    echo "❌ .NET installation failed or is corrupted!"
    echo "Attempting to diagnose..."
    ls -la ${DOTNET_INSTALL_DIR} 2>&1 || echo "Install directory does not exist"
    ls -la /usr/bin/dotnet 2>&1 || echo "Symlink does not exist"
    exit 1
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
