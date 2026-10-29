#!/bin/bash
set -e
mkdir -p /var/www/agenticpitstop
if ! command -v dotnet >/dev/null 2>&1; then
  dnf install -y aspnetcore-runtime-8.0
fi