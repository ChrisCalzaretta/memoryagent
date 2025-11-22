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

# Keep the server running
wait $OLLAMA_PID

