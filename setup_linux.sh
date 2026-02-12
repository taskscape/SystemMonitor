#!/bin/bash
set -e

# Colors
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Check for root
if [ "$EUID" -ne 0 ]; then
  echo -e "${RED}Please run as root (use sudo ./setup_linux.sh)${NC}"
  exit 1
fi

echo -e "${BLUE}=========================================${NC}"
echo -e "${BLUE}   System Monitor - Linux Installer      ${NC}"
echo -e "${BLUE}=========================================${NC}"

echo "Select installation type:"
echo "1) Server (Collector)"
echo "2) Client (Monitor Agent)"
echo "3) Full Suite (Both)"
read -p "Enter choice [1-3]: " choice

INSTALL_DIR="/opt/system-monitor"

# Function to install Server
install_server() {
    echo -e "
${GREEN}>>> Installing Server (Collector)...${NC}"
    
    echo "Building Server..."
    dotnet publish "SystemCollectorService/SystemCollectorService.csproj" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o "$INSTALL_DIR/server"

    echo "Creating Systemd Service..."
    cat > /etc/systemd/system/sysmon-server.service <<EOF
[Unit]
Description=System Monitor Collector Service
After=network.target

[Service]
Type=simple
ExecStart=$INSTALL_DIR/server/SystemCollectorService
WorkingDirectory=$INSTALL_DIR/server
Restart=always
RestartSec=10
User=root
Environment=ASPNETCORE_URLS=https://0.0.0.0:5101

[Install]
WantedBy=multi-user.target
EOF

    echo "Configuring Firewall (5101)..."
    if command -v ufw > /dev/null; then
        ufw allow 5101/tcp
    fi

    systemctl daemon-reload
    systemctl enable sysmon-server
    systemctl restart sysmon-server
    
    echo -e "${GREEN}Server installed and started! Dashboard: https://<THIS_IP>:5101${NC}"
}

# Function to install Client
install_client() {
    echo -e "
${GREEN}>>> Installing Client (Agent)...${NC}"
    
    read -p "Enter System Monitor Server URL (e.g., https://192.168.1.50:5101): " SERVER_URL
    # Remove trailing slash if present
    SERVER_URL=${SERVER_URL%/}

    echo "Building Client..."
    dotnet publish "SystemMonitorService/SystemMonitorService.csproj" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o "$INSTALL_DIR/client"

    echo "Configuring Client..."
    # Update appsettings.json with sed
    sed -i "s|https://localhost:5101|$SERVER_URL|g" "$INSTALL_DIR/client/appsettings.json"
    # Also update the API path specific config if needed
    sed -i "s|"CollectorEndpoint": ".*"|"CollectorEndpoint": "$SERVER_URL/api/v1/metrics"|g" "$INSTALL_DIR/client/appsettings.json"

    echo "Creating Systemd Service..."
    cat > /etc/systemd/system/sysmon-client.service <<EOF
[Unit]
Description=System Monitor Agent Service
After=network.target

[Service]
Type=simple
ExecStart=$INSTALL_DIR/client/SystemMonitorService
WorkingDirectory=$INSTALL_DIR/client
Restart=always
RestartSec=10
User=root

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable sysmon-client
    systemctl restart sysmon-client

    echo -e "${GREEN}Client installed and started! Reporting to: $SERVER_URL${NC}"
}

# Execution Logic
case $choice in
    1)
        install_server
        ;;
    2)
        install_client
        ;;
    3)
        install_server
        install_client
        ;;
    *)
        echo "Invalid choice."
        exit 1
        ;;
esac

echo -e "
${BLUE}Installation Complete.${NC}"
