const {
  parseNdjsonLines,
  appendMessage,
  startPolling,
  setFetch,
} = require('../../src/MessageStreamApp/wwwroot/app.js');

describe('NDJSON parsing', () => {
  test('splits lines and keeps remainder', () => {
    const input = '{"id":1}\n{"id":2}\npartial';
    const { messages, remainder } = parseNdjsonLines(input);
    expect(messages).toHaveLength(2);
    expect(messages[0].id).toBe(1);
    expect(messages[1].id).toBe(2);
    expect(remainder).toBe('partial');
  });
});

describe('appendMessage', () => {
  test('appends element to messages container', () => {
    document.body.innerHTML = '<div id="messages"></div>';
    const now = new Date().toISOString();
    appendMessage({ timestamp: now, content: 'hello' });
    const nodes = document.querySelectorAll('#messages .message');
    expect(nodes.length).toBe(1);
    expect(nodes[0].textContent).toContain('hello');
  });
});

describe('startPolling', () => {
  test('fetches messages and appends to DOM', async () => {
    jest.useFakeTimers();
    document.body.innerHTML = '<div id="messages"></div>';

    const payload = [
      { id: 1, timestamp: new Date().toISOString(), content: 'one' },
      { id: 2, timestamp: new Date().toISOString(), content: 'two' },
    ];

    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => payload,
    });

    setFetch(fetchMock);
    await startPolling(10);

    jest.advanceTimersByTime(50);
    await Promise.resolve();
    await Promise.resolve();

    const nodes = document.querySelectorAll('#messages .message');
    expect(fetchMock).toHaveBeenCalled();
    expect(nodes.length).toBeGreaterThanOrEqual(2);

    jest.useRealTimers();
  });
});
