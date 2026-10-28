#!/bin/bash
if systemctl list-unit-files | grep -q '^agenticpitstop.service'; then
  systemctl stop agenticpitstop || true
fi
exit 0
