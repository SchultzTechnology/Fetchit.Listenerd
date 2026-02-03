# Fetchit.Listenerd

A lightweight SIP packet capture and MQTT forwarding service designed for Raspberry Pi and other ARM64 Linux systems.

## Features

- **SIP Packet Capture**: Captures SIP packets from network interfaces using libpcap
- **MQTT Integration**: Forwards captured packets to MQTT broker
- **Web Configuration Interface**: Easy-to-use web UI for configuring MQTT settings (port 8080)
- **Self-Contained Deployment**: Pre-built ARM64 binaries with no .NET SDK installation required
- **Supervisor Management**: Automatic service restart and logging

## Quick Deployment

Deploy Fetchit.Listenerd with a single command:

```bash
sudo bash -c "$(curl -fsSL https://raw.githubusercontent.com/SchultzTechnology/Fetchit.Listenerd/main/deploy.sh)"
```

Or using wget:

```bash
sudo bash -c "$(wget -qO- https://raw.githubusercontent.com/SchultzTechnology/Fetchit.Listenerd/main/deploy.sh)"
```

This will:
- Download the installation script
- Install system dependencies (libpcap, supervisor, iptables)
- Download the latest pre-built ARM64 release from GitHub
- Extract binaries to `/app/fetchit/`
- Configure and start services via Supervisor
- Set up firewall rules for port 8080

After deployment, access the web interface at `http://localhost:8080`

## System Requirements

- **Operating System**: ARM64 Linux (Raspberry Pi OS 64-bit, Ubuntu ARM64, etc.)
  - Supported: Ubuntu, Debian, Raspbian, Fedora, RHEL, CentOS, Rocky Linux, AlmaLinux, Arch, Alpine
- **Architecture**: ARM64 (aarch64)
  - For 32-bit ARM systems, see manual build instructions below
- **Permissions**: Root access (sudo) required for installation
- **Network**: Internet connection for downloading releases
- **Dependencies**: libpcap library (automatically installed)

## Build from Source

### GitHub Actions (Recommended)

This project uses GitHub Actions to automatically build self-contained ARM64 binaries:

1. **Automatic Builds**: Push a tag starting with `v` (e.g., `v1.0.0`) to trigger a release build
2. **Manual Builds**: Use the "Actions" tab on GitHub to manually trigger the workflow
3. **Downloads**: Pre-built binaries are available on the [Releases](https://github.com/SchultzTechnology/Fetchit.Listenerd/releases) page

The build process:
- Builds both `Fetchit.Listenerd` (worker service) and `Fetchit.WebPage` (web UI)
- Uses `--runtime linux-arm64 --self-contained` for standalone deployment
- Applies `PublishSingleFile` and `PublishTrimmed` optimizations
- Packages both projects into a single `fetchit-linux-arm64.zip`

### Local Development Build

For development or custom builds:

```bash
# Clone the repository
git clone https://github.com/SchultzTechnology/Fetchit.Listenerd.git
cd Fetchit.Listenerd

# Install .NET 9.0 SDK
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 9.0

# Build for ARM64 (self-contained)
dotnet publish Fetchit.Listenerd/Fetchit.Listenerd.csproj \
  --configuration Release \
  --runtime linux-arm64 \
  --self-contained \
  --output ./publish/listenerd

dotnet publish Fetchit.WebPage/Fetchit.WebPage.csproj \
  --configuration Release \
  --runtime linux-arm64 \
  --self-contained \
  --output ./publish/webpage

# For 32-bit ARM (Pi OS 32-bit)
# Use --runtime linux-arm instead of linux-arm64
```

## Manual Installation

If you prefer manual installation or need to customize the process:

```bash
# 1. Download the latest release
wget https://github.com/SchultzTechnology/Fetchit.Listenerd/releases/latest/download/fetchit-linux-arm64.zip

# 2. Install system dependencies
sudo apt-get update
sudo apt-get install -y libpcap0.8 supervisor iptables-persistent wget unzip

# 3. Extract binaries
sudo mkdir -p /app/fetchit
sudo unzip fetchit-linux-arm64.zip -d /app/fetchit

# 4. Set executable permissions
sudo chmod +x /app/fetchit/listenerd/Fetchit.Listenerd
sudo chmod +x /app/fetchit/webpage/Fetchit.WebPage

# 5. Create data directory
sudo mkdir -p /app/data

# 6. Configure firewall
sudo iptables -A INPUT -p tcp --dport 8080 -j ACCEPT
sudo netfilter-persistent save

# 7. Create Supervisor configuration
sudo tee /etc/supervisor/conf.d/fetchit.conf > /dev/null <<EOF
[program:fetchit-listenerd]
command=/app/fetchit/listenerd/Fetchit.Listenerd
directory=/app/fetchit/listenerd
autostart=true
autorestart=true
stderr_logfile=/var/log/supervisor/listenerd.err.log
stdout_logfile=/var/log/supervisor/listenerd.out.log

[program:fetchit-webpage]
command=/app/fetchit/webpage/Fetchit.WebPage
directory=/app/fetchit/webpage
autostart=true
autorestart=true
environment=ASPNETCORE_URLS="http://0.0.0.0:8080",ASPNETCORE_ENVIRONMENT="Production"
stderr_logfile=/var/log/supervisor/webpage.err.log
stdout_logfile=/var/log/supervisor/webpage.out.log
EOF

# 8. Start services
sudo systemctl enable supervisor
sudo systemctl restart supervisor
sudo supervisorctl reread
sudo supervisorctl update
```

## Service Management

```bash
# Check service status
sudo supervisorctl status

# View logs
sudo tail -f /var/log/supervisor/listenerd.out.log
sudo tail -f /var/log/supervisor/webpage.out.log

# Restart services
sudo supervisorctl restart fetchit-listenerd
sudo supervisorctl restart fetchit-webpage

# Stop services
sudo supervisorctl stop fetchit-listenerd fetchit-webpage
```

## Database Location

SQLite database is stored at `/app/data/mqttconfig.db`

## Ports

- **8080**: Web configuration interface (HTTP)
- **5060**: SIP packet capture (UDP) - monitored by listenerd service

## Docker Deployment

For containerized deployment, use the included Docker configuration:

```bash
docker-compose up -d
```

See `docker-compose.yml` for details.

## Troubleshooting

### Release Download Fails

If the installation script can't download the release:
1. Verify internet connectivity: `ping github.com`
2. Check if a release exists: Visit the [Releases page](https://github.com/SchultzTechnology/Fetchit.Listenerd/releases)
3. Ensure you're using ARM64 Linux: `uname -m` should show `aarch64`

### Permission Errors

The packet capture service requires elevated privileges:
- Ensure Supervisor is running as root
- Verify executable permissions: `ls -la /app/fetchit/*/Fetchit.*`

### libpcap Not Found

If you see libpcap-related errors:
```bash
# Ubuntu/Debian
sudo apt-get install libpcap0.8

# Fedora/RHEL
sudo dnf install libpcap
```

## License

[Add your license information here]

## Contributing

[Add contribution guidelines here]
