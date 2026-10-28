#!/bin/bash

echo "===== Installing Dependencies ====="

# Update system
sudo dnf update -y

# Install .NET 8 Runtime
sudo rpm -Uvh https://packages.microsoft.com/config/centos/9/packages-microsoft-prod.rpm

sudo dnf install -y dotnet-sdk-8.0

# Install Nginx
sudo dnf install -y nginx

# Enable Nginx
sudo systemctl enable nginx
sudo systemctl start nginx

# Create application directory
sudo mkdir -p /var/www/pitstop

# Set permissions
sudo chmod -R 755 /var/www/pitstop

echo "===== Installation Completed ====="