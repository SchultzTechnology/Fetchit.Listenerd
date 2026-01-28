#!/bin/bash

set -e

echo "=========================================="
echo "  Fetchit.Listenerd Deployment Script"
echo "=========================================="
echo ""

REPO_URL="https://github.com/SchultzTechnology/Fetchit.Listenerd.git"

# Ensure script is run as root
if ["$EUID" -ne 0 ]; then
  echo "❌ Please run this script as root (use sudo)"
  exit 1
fi

# Install git if not present
if ! command -v git &> /dev/null; then
    echo "Installing git..."
    apt-get update
    apt-get install -y git
fi

# Clone or update repository
git clone $REPO_URL --depth 1 ~/Fetchit.Listenerd || {
    echo "Repository already exists. Pulling latest changes..."
    cd ~/Fetchit.Listenerd
    git pull origin main
}

# Navigate to repository and run start.sh
cd ~/Fetchit.Listenerd
echo "Running installation script..."
chmod +x start.sh
./start.sh

echo "✅ Deployment completed successfully!"