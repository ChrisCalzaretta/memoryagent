#!/bin/bash
set -e

# Start Ollama server in background
/bin/ollama serve &
OLLAMA_PID=$!

# Wait for Ollama to be ready
echo "Waiting for Ollama to start..."
sleep 5

# Pull the embedding model if not already present
echo "Checking for mxbai-embed-large model..."
if ! ollama list | grep -q "mxbai-embed-large"; then
    echo "Pulling mxbai-embed-large model..."
    ollama pull mxbai-embed-large:latest
    echo "Model pulled successfully!"
else
    echo "Model already exists."
fi
if ! ollama list | grep -q "deepseek-coder-v2"; then
    echo "Pulling pull deepseek-coder-v2:16b model..."
    ollama pull deepseek-coder-v2:16b
    
    echo "Model pulled successfully!"
else
    echo "Model already exists."
fi

# Pre-load DeepSeek model into VRAM in background (non-blocking)
echo "Pre-loading deepseek-coder-v2:16b into VRAM (background)..."
(sleep 10 && ollama run deepseek-coder-v2:16b "test" > /dev/null 2>&1 && echo "Model preloaded successfully!") &

# Keep the server running
wait $OLLAMA_PID

