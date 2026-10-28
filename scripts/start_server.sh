#!/bin/bash
set -euo pipefail

APP_DIR=/var/www/agenticpitstop
APP_DLL=AgenticPitstop.dll        # must match the real main DLL in the publish output
SERVICE=agenticpitstop

# Fail early with a clear message if the DLL name is wrong
if [ ! -f "$APP_DIR/$APP_DLL" ]; then
  echo "ERROR: $APP_DIR/$APP_DLL not found. Contents of $APP_DIR:"
  ls -la "$APP_DIR"
  exit 1
fi

DOTNET_BIN=$(command -v dotnet || true)
if [ -z "$DOTNET_BIN" ]; then
  echo "ERROR: dotnet runtime not installed"
  exit 1
fi

cat > /etc/systemd/system/${SERVICE}.service <<EOF
[Unit]
Description=Agentic Pitstop
After=network.target

[Service]
WorkingDirectory=${APP_DIR}
ExecStart=${DOTNET_BIN} ${APP_DIR}/${APP_DLL}
Restart=always
RestartSec=5
User=root
SyslogIdentifier=${SERVICE}
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_CLI_TELEMETRY_OPTOUT=1

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable "$SERVICE"
systemctl restart "$SERVICE"

# Confirm it actually stayed up, otherwise show why
sleep 3
if ! systemctl is-active --quiet "$SERVICE"; then
  echo "ERROR: $SERVICE failed to start"
  journalctl -u "$SERVICE" -n 50 --no-pager
  exit 1
fi
