#!/bin/bash
set -e
cat > /etc/systemd/system/agenticpitstop.service <<'EOF'
[Unit]
Description=Agentic Pitstop
After=network.target

[Service]
WorkingDirectory=/var/www/agenticpitstop
ExecStart=/usr/bin/dotnet /var/www/agenticpitstop/AgenticPitstop.dll
Restart=always
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable agenticpitstop
systemctl restart agenticpitstop
