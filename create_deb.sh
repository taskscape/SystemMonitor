#!/bin/bash
set -e

# Configuration
VERSION="1.1.0"
ARCH="amd64"

create_package() {
    local PKG_NAME=$1
    local PROJECT_PATH=$2
    local BINARY_NAME=$3
    local DIR="pkg_${PKG_NAME}"

    echo "Building Package: ${PKG_NAME}..."

    # 1. Clean and Create structure
    rm -rf "$DIR"
    mkdir -p "$DIR/DEBIAN"
    mkdir -p "$DIR/usr/bin"
    mkdir -p "$DIR/etc/${PKG_NAME}"
    mkdir -p "$DIR/lib/systemd/system"

    # 2. Build the .NET App (Self-Contained)
    dotnet publish "$PROJECT_PATH" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o "publish_temp"
    
    # 3. Copy files
    cp "publish_temp/$BINARY_NAME" "$DIR/usr/bin/${PKG_NAME}"
    cp "publish_temp/appsettings.json" "$DIR/etc/${PKG_NAME}/appsettings.json"
    rm -rf "publish_temp"

    # 4. Create Control file
    cat > "$DIR/DEBIAN/control" <<EOF
Package: ${PKG_NAME}
Version: ${VERSION}
Section: utils
Priority: optional
Architecture: ${ARCH}
Maintainer: SystemMonitor Team
Description: Standalone System Monitoring Suite
EOF

    # 5. Create Systemd Service
    if [ "$PKG_NAME" == "system-monitor-server" ]; then
        SERVICE_DESC="System Monitor Collector"
        EXEC="/usr/bin/system-monitor-server"
    else
        SERVICE_DESC="System Monitor Agent"
        EXEC="/usr/bin/system-monitor-agent"
    fi

    cat > "$DIR/lib/systemd/system/${PKG_NAME}.service" <<EOF
[Unit]
Description=${SERVICE_DESC}
After=network.target

[Service]
Type=simple
ExecStart=${EXEC}
WorkingDirectory=/etc/${PKG_NAME}
Restart=always
RestartSec=10
User=root

[Install]
WantedBy=multi-user.target
EOF

    # 6. Create Post-Install script (to start service automatically)
    cat > "$DIR/DEBIAN/postinst" <<EOF
#!/bin/bash
systemctl daemon-reload
systemctl enable ${PKG_NAME}
systemctl start ${PKG_NAME}
echo "${PKG_NAME} installed and started successfully."
EOF
    chmod 755 "$DIR/DEBIAN/postinst"

    # 7. Build the .deb
    dpkg-deb --build "$DIR" "${PKG_NAME}_${VERSION}_${ARCH}.deb"
    rm -rf "$DIR"
    echo "Created: ${PKG_NAME}_${VERSION}_${ARCH}.deb"
}

# Run the packaging
create_package "system-monitor-server" "SystemCollectorService/SystemCollectorService.csproj" "SystemCollectorService"
create_package "system-monitor-agent" "SystemMonitorService/SystemMonitorService.csproj" "SystemMonitorService"

echo -e "
Successfully created .deb packages!"
