#!/bin/bash

set -e

echo "=========================================="
echo "  Fetchit.Listenerd Deployment Script"
echo "=========================================="
echo ""

REPO_URL="https://github.com/SchultzTechnology/Fetchit.Listenerd.git"
INSTALL_DIR="/tmp/fetchit-install"

# Ensure script is run as root
if [ "$EUID" -ne 0 ]; then
  echo "❌ Please run this script as root (use sudo)"
  exit 1
fi

# Install git if not present
if ! command -v git &> /dev/null; then
    echo "Installing git..."
    if command -v apt-get &> /dev/null; then
        # Clean apt cache first if needed
        apt-get clean 2>/dev/null || true
        
        # Try to update package lists
        echo "Updating package lists..."
        if ! apt-get update 2>&1; then
            echo "⚠ Warning: apt-get update failed, attempting to clean and retry..."
            rm -rf /var/lib/apt/lists/* 2>/dev/null || true
            mkdir -p /var/lib/apt/lists/partial 2>/dev/null || true
            apt-get update 2>&1 || echo "⚠ Warning: apt-get update still failing, will try to install git anyway..."
        fi
        
        apt-get install -y git
    elif command -v dnf &> /dev/null; then
        dnf install -y git
    elif command -v yum &> /dev/null; then
        yum install -y git
    elif command -v pacman &> /dev/null; then
        pacman -Sy --noconfirm git
    else
        echo "❌ Unable to install git automatically. Please install git and try again."
        exit 1
    fi
fi

# Clone repository to temporary location
echo "Downloading installation script..."
rm -rf ${INSTALL_DIR}
git clone $REPO_URL ${INSTALL_DIR} --depth 1

echo "Running installation script..."
cd ${INSTALL_DIR}
chmod +x start.sh
./start.sh

# Clean up
cd /
rm -rf ${INSTALL_DIR}

echo "✅ Deployment completed successfully!"