#!/bin/bash
for i in {1..10}; do
  if curl -fs http://localhost:5000/ >/dev/null; then exit 0; fi
  sleep 3
done
echo "App did not respond on port 5000"
systemctl status agenticpitstop --no-pager
exit 1