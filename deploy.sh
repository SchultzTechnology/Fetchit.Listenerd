#!/bin/bash
set -e

echo "=========================================="
echo "  Fetchit.Listenerd Deployment Script"
echo "=========================================="
echo ""

#--------------------------------------------
# Variables
#--------------------------------------------
REPO_URL="https://github.com/SchultzTechnology/Fetchit.Listenerd.git"
INSTALL_DIR=~/Fetchit.Listenerd

#--------------------------------------------
# Ensure script is run as root
#--------------------------------------------
if [ "$EUID" -ne 0 ]; then
  echo "❌ Please run this script as root (use sudo)"
  exit 1
fi

#--------------------------------------------
# Install git if not present
#--------------------------------------------
if ! command -v git &> /dev/null; then
    echo "Installing git..."
    apt-get update
    apt-get install -y git
fi

#--------------------------------------------
# Clone or update repository
#--------------------------------------------
if [ -d "${INSTALL_DIR}" ]; then
    echo "Repository already exists. Pulling latest changes..."
    cd ${INSTALL_DIR}
    git pull
else
    echo "Cloning repository from ${REPO_URL}..."
    git clone ${REPO_URL} ${INSTALL_DIR}
fi

#--------------------------------------------
# Navigate to repository and run start.sh
#--------------------------------------------
cd ${INSTALL_DIR}

echo ""
echo "Running installation script..."
chmod +x start.sh
./start.sh

echo ""
echo "=========================================="
echo "✅ Deployment completed successfully!"
echo "=========================================="