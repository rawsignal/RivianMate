#!/bin/bash
# Docker installation script for WSL2 Ubuntu
# Run this inside your WSL2 Ubuntu terminal

set -e

echo "=== Installing Docker in WSL2 ==="

# Update package index
sudo apt-get update

# Install prerequisites
sudo apt-get install -y ca-certificates curl gnupg

# Add Docker's official GPG key
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

# Add Docker repository
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Install Docker
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Add current user to docker group (so you don't need sudo)
sudo usermod -aG docker $USER

# Start Docker service
sudo service docker start

echo ""
echo "=== Docker installed successfully! ==="
echo ""
echo "IMPORTANT: Log out and back into WSL for group changes to take effect:"
echo "  1. Type 'exit' to leave WSL"
echo "  2. Run 'wsl --shutdown' in PowerShell"
echo "  3. Open WSL again"
echo ""
echo "Then test with: docker run hello-world"
