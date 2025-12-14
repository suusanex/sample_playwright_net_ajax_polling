/* global window, document, fetch */

// フロントエンド用のシンプルな状態管理
const state = {
    pollingTimer: null,
    fetchImpl: null,
};

function getFetch() {
    if (state.fetchImpl) {
        return state.fetchImpl;
    }

    return (...args) => fetch(...args);
}

function setFetch(impl) {
    state.fetchImpl = impl;
}

function clearPollingTimer() {
    if (state.pollingTimer) {
        clearInterval(state.pollingTimer);
        state.pollingTimer = null;
    }
}

function appendMessage(message) {
    const container = document.getElementById("messages");
    if (!container) {
        console.error("Missing messages container");
        return;
    }

    const div = document.createElement("div");
    div.className = "message";

    const timestamp = new Date(message.timestamp);
    div.textContent = `[${timestamp.toISOString()}] ${message.content}`;
    container.appendChild(div);
}

function parseNdjsonLines(buffer) {
    // NDJSON文字列を行単位で分割してパースする
    const lines = buffer.split("\n");
    const incomplete = lines.pop() ?? "";
    const messages = [];

    for (const line of lines) {
        const trimmed = line.trim();
        if (!trimmed) {
            continue;
        }

        messages.push(JSON.parse(trimmed));
    }

    return { messages, remainder: incomplete };
}

async function startPolling(intervalMs) {
    clearPollingTimer();
    const fetchImpl = getFetch();
    let isPolling = false;

    const pollOnce = async () => {
        if (isPolling) {
            return;
        }

        isPolling = true;
        try {
            const response = await fetchImpl("/api/messages/poll");
            if (!response.ok) {
                console.error("Polling failed", response.status);
                return;
            }

            const messages = await response.json();
            messages.forEach(appendMessage);
        } catch (error) {
            console.error("Polling error", error);
        } finally {
            isPolling = false;
        }
    };

    await pollOnce();
    state.pollingTimer = setInterval(pollOnce, intervalMs);
}

async function startStreaming(config) {
    const fetchImpl = getFetch();
    const response = await fetchImpl("/api/messages/stream");
    if (!response.ok || !response.body) {
        throw new Error(`Streaming failed: status ${response.status}`);
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";

    while (true) {
        const { done, value } = await reader.read();
        if (done) {
            break;
        }

        buffer += decoder.decode(value, { stream: true });
        const { messages, remainder } = parseNdjsonLines(buffer);
        buffer = remainder;
        messages.forEach(appendMessage);

        if (config && typeof config.clientFetchIntervalMs === "number") {
            await new Promise(resolve => setTimeout(resolve, config.clientFetchIntervalMs));
        }
    }

    if (buffer.trim()) {
        appendMessage(JSON.parse(buffer));
    }
}

async function init() {
    const fetchImpl = getFetch();
    const configResponse = await fetchImpl("/api/config");
    const configText = await configResponse.text();
    if (!configResponse.ok) {
        throw new Error(`Config fetch failed: status ${configResponse.status}`);
    }

    let config;
    try {
        config = JSON.parse(configText);
    } catch (error) {
        console.error("init: failed to parse config", error, configText);
        throw error;
    }
    const configDisplay = document.getElementById("config-display");
    if (configDisplay) {
        configDisplay.textContent = `Mode: ${config.mode}, Generation: ${config.messageGenerationIntervalMs}ms, Fetch: ${config.clientFetchIntervalMs}ms`;
    }

    if (config.mode === "Streaming") {
        startStreaming(config).catch(error => console.error("Streaming error", error));
    } else {
        await startPolling(config.clientFetchIntervalMs);
    }
}

if (typeof window !== "undefined") {
    window.addEventListener("DOMContentLoaded", () => {
        init().catch(error => console.error("Initialization error", error));
    });
}

// Node.js環境でのテスト用エクスポート
if (typeof module !== "undefined") {
    module.exports = {
        appendMessage,
        parseNdjsonLines,
        startPolling,
        startStreaming,
        init,
        setFetch,
    };
}
