import {
  AI_STREAM_SCHEMA_VERSION,
  parseAiStreamEventPayload,
  parseAiStreamSseLine,
} from './ai-stream-contract';

describe('ai-stream-contract', () => {
  it('parses a versioned token event', () => {
    const event = parseAiStreamEventPayload({
      schemaVersion: AI_STREAM_SCHEMA_VERSION,
      type: 'token',
      token: 'Merhaba',
      fallback: { used: false, reason: null },
    });

    expect(event).toEqual({
      schemaVersion: AI_STREAM_SCHEMA_VERSION,
      type: 'token',
      token: 'Merhaba',
      fallback: { used: false, reason: null },
    });
  });

  it('parses a completion event with completion metadata', () => {
    const event = parseAiStreamEventPayload({
      schemaVersion: AI_STREAM_SCHEMA_VERSION,
      type: 'done',
      completion: { status: 'completed' },
      fallback: { used: false, reason: null },
    });

    expect(event).toEqual({
      schemaVersion: AI_STREAM_SCHEMA_VERSION,
      type: 'done',
      completion: { status: 'completed' },
      fallback: { used: false, reason: null },
    });
  });

  it('parses fallback metadata from an SSE line', () => {
    const event = parseAiStreamSseLine(
      'data: {"schemaVersion":1,"type":"token","token":"Yedek yanit","fallback":{"used":true,"reason":"zero_tokens"}}'
    );

    expect(event).toEqual({
      schemaVersion: AI_STREAM_SCHEMA_VERSION,
      type: 'token',
      token: 'Yedek yanit',
      fallback: { used: true, reason: 'zero_tokens' },
    });
  });
});
