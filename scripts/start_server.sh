#!/bin/bash

echo "===== Starting Application ====="

cd /var/www/pitstop

nohup dotnet AgenticPitstop.Web.dll \
> application.log 2>&1 &

echo "Application Started Successfully"