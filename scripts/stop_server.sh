#!/bin/bash

echo "===== Stopping Existing Application ====="

PID=$(pgrep -f AgenticPitstop.Web.dll)

if [ ! -z "$PID" ]; then
    echo "Killing process $PID"
    sudo kill -9 $PID
else
    echo "No running application found"
fi

echo "===== Application Stopped ====="