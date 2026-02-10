#!/bin/bash

# Configuration
REPO="taskscape/SystemMonitor"
FILE="system-monitor-agent_1.1.0_amd64.deb"
DOWNLOAD_URL="https://github.com/$REPO/releases/latest/download/$FILE"

echo ">>> Downloading System Monitor Agent..."
wget -q --show-progress -O agent.deb "$DOWNLOAD_URL"

if [ $? -ne 0 ]; then
    echo "Error: Failed to download package. Check if the release exists on GitHub."
    exit 1
fi

echo ">>> Installing via apt..."
sudo apt update
sudo apt install -y ./agent.deb

echo ">>> Cleaning up..."
rm agent.deb

echo ">>> Installation Complete!"
echo ">>> Edit config: sudo nano /etc/system-monitor-agent/appsettings.json"
echo ">>> Restart service: sudo systemctl restart system-monitor-agent"
