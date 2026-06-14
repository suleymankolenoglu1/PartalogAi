import {
  extractChatStreamEvents,
  flushChatStreamBuffer,
  parseChatStreamPayload,
} from './ai-stream-contract';

describe('ai stream contract', () => {
  it('replays sources, token and done events from SSE frames', () => {
    const sse = [
      'data: {"schemaVersion":1,"type":"sources","fallback":{"used":false},"sources":[{"code":"4109410"}]}',
      '',
      'data: {"schemaVersion":1,"type":"token","fallback":{"used":false},"token":"Merhaba"}',
      '',
      'data: {"schemaVersion":1,"type":"done","fallback":{"used":false},"completion":{"status":"completed"}}',
      '',
      '',
    ].join('\n');

    const result = extractChatStreamEvents(sse);

    expect(result.errors).toEqual([]);
    expect(result.remaining).toBe('');
    expect(result.events.map((event) => event.type)).toEqual(['sources', 'token', 'done']);
    expect(result.events[0].type === 'sources' && result.events[0].sources[0].code).toBe('4109410');
  });

  it('keeps incomplete frames in the remaining buffer', () => {
    const first = extractChatStreamEvents(
      'data: {"schemaVersion":1,"type":"token","fallback":{"used":false},"token":"Mer'
    );
    expect(first.events).toEqual([]);
    expect(first.remaining).toContain('"token":"Mer');

    const second = extractChatStreamEvents(`${first.remaining}haba"}\n\n`);
    expect(second.errors).toEqual([]);
    expect(second.events.length).toBe(1);
    expect(second.events[0].type === 'token' && second.events[0].token).toBe('Merhaba');
  });

  it('rejects schema drift before the UI consumes the stream', () => {
    expect(() =>
      parseChatStreamPayload(
        '{"schemaVersion":2,"type":"token","fallback":{"used":false},"token":"x"}'
      )
    ).toThrowError(/schemaVersion/);

    expect(() =>
      parseChatStreamPayload('{"schemaVersion":1,"type":"token","fallback":{"used":false}}')
    ).toThrowError(/token/);
  });

  it('flushes a final data line when the stream closes without a blank frame delimiter', () => {
    const result = flushChatStreamBuffer(
      'data: {"schemaVersion":1,"type":"done","fallback":{"used":false},"completion":{"status":"completed"}}'
    );

    expect(result.errors).toEqual([]);
    expect(result.events.map((event) => event.type)).toEqual(['done']);
  });
});
