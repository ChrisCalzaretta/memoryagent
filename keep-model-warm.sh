#!/bin/bash
# Keep DeepSeek model loaded in VRAM by periodically pinging it

echo "Starting model warm-up service..."

# Initial load
echo "Initial model load..."
echo "test" | ollama run deepseek-coder-v2:16b > /dev/null 2>&1

echo "Model loaded! Keeping warm with periodic pings..."

# Keep alive forever
while true; do
    sleep 300  # Every 5 minutes
    echo "ping" | timeout 60 ollama run deepseek-coder-v2:16b > /dev/null 2>&1 || true
done






