#!/bin/bash
set -e

# Start Ollama server in background
/bin/ollama serve &
OLLAMA_PID=$!

# Wait for Ollama API to be fully ready using ollama list command
echo "Waiting for Ollama API to be ready..."
MAX_RETRIES=30
RETRY_COUNT=0
until ollama list > /dev/null 2>&1; do
    RETRY_COUNT=$((RETRY_COUNT + 1))
    if [ $RETRY_COUNT -ge $MAX_RETRIES ]; then
        echo "ERROR: Ollama failed to start after $MAX_RETRIES attempts"
        exit 1
    fi
    echo "  Waiting for Ollama... (attempt $RETRY_COUNT/$MAX_RETRIES)"
    sleep 2
done
echo "Ollama API is ready!"

# Give Ollama extra time to fully index existing models from the mounted volume
echo "Allowing time for model indexing from volume..."
sleep 5

# Check and pull embedding model if needed
echo "Checking for mxbai-embed-large model..."
MODELS=$(ollama list 2>/dev/null || echo "")
echo "Currently available models:"
echo "$MODELS"

if echo "$MODELS" | grep -q "mxbai-embed-large"; then
    echo "✓ mxbai-embed-large already exists - skipping pull"
else
    echo "Pulling mxbai-embed-large model..."
    ollama pull mxbai-embed-large:latest
    echo "✓ Model pulled successfully!"
fi

# Check and pull LLM model if needed
echo "Checking for deepseek-coder-v2 model..."
if echo "$MODELS" | grep -q "deepseek-coder-v2"; then
    echo "✓ deepseek-coder-v2 already exists - skipping pull"
else
    echo "Pulling deepseek-coder-v2:16b model..."
    ollama pull deepseek-coder-v2:16b
    echo "✓ Model pulled successfully!"
fi

# Pre-load DeepSeek model into VRAM in background (non-blocking)
echo "Pre-loading deepseek-coder-v2:16b into VRAM (background)..."
(sleep 10 && ollama run deepseek-coder-v2:16b "test" > /dev/null 2>&1 && echo "✓ Model preloaded into VRAM!") &

# Keep the server running
wait $OLLAMA_PID

