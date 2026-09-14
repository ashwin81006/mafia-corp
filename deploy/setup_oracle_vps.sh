#!/bin/bash
set -e

echo "=========================================================="
echo "    MafiaCorp - Oracle Cloud VPS Setup & Deploy Script    "
echo "=========================================================="

# 1. Update system packages
echo "[1/4] Updating packages..."
sudo apt-get update -y
sudo apt-get install -y ca-certificates curl gnupg lsb-release iptables-persistent

# 2. Configure Oracle Cloud Ubuntu iptables firewall
# (Oracle Cloud default Ubuntu image blocks incoming traffic even if open in Cloud Console!)
echo "[2/4] Opening ports 5130 (API) and 54555 (GameServer) in iptables..."
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 5130 -j ACCEPT || true
sudo iptables -I INPUT 6 -m state --state NEW -p tcp --dport 54555 -j ACCEPT || true
sudo netfilter-persistent save || true

# 3. Install Docker and Docker Compose
echo "[3/4] Installing Docker..."
if ! command -v docker &> /dev/null; then
    curl -fsSL https://get.docker.com -o get-docker.sh
    sudo sh get-docker.sh
    sudo usermod -aG docker $USER
    rm get-docker.sh
fi

# 4. Build and start services
echo "[4/4] Starting MafiaCorp Stack with Docker Compose..."
cd "$(dirname "$0")"
sudo docker compose up -d --build

echo "=========================================================="
echo "✅ MafiaCorp Server is now running 24/7!"
echo "   - Web API: http://$(curl -s ifconfig.me):5130"
echo "   - TCP GameServer: $(curl -s ifconfig.me):54555"
echo "=========================================================="
