export interface ChatStreamFallback {
  used: boolean;
  reason?: string | null;
}

export interface ChatStreamCompletion {
  status: string;
}

export interface ChatStreamSourcesEvent {
  schemaVersion: 1;
  type: 'sources';
  fallback: ChatStreamFallback;
  sources: any[];
  debugIntent?: any;
  searchTrace?: any;
}

export interface ChatStreamTokenEvent {
  schemaVersion: 1;
  type: 'token';
  fallback: ChatStreamFallback;
  token: string;
}

export interface ChatStreamDoneEvent {
  schemaVersion: 1;
  type: 'done';
  fallback: ChatStreamFallback;
  completion: ChatStreamCompletion;
}

export type ChatStreamEvent = ChatStreamSourcesEvent | ChatStreamTokenEvent | ChatStreamDoneEvent;

export interface ChatStreamParseResult {
  events: ChatStreamEvent[];
  remaining: string;
  errors: string[];
}

export function parseChatStreamPayload(payload: string): ChatStreamEvent {
  let parsed: any;
  try {
    parsed = JSON.parse(payload);
  } catch (error) {
    throw new Error(`Invalid chat stream JSON payload: ${(error as Error).message}`);
  }

  if (parsed?.schemaVersion !== 1) {
    throw new Error('Unsupported chat stream schemaVersion.');
  }

  if (!parsed.fallback || typeof parsed.fallback.used !== 'boolean') {
    throw new Error('Missing chat stream fallback contract.');
  }

  switch (parsed.type) {
    case 'sources':
      if (!Array.isArray(parsed.sources)) {
        throw new Error('sources event must include a sources array.');
      }
      return parsed as ChatStreamSourcesEvent;

    case 'token':
      if (typeof parsed.token !== 'string' || parsed.token.length === 0) {
        throw new Error('token event must include non-empty token text.');
      }
      return parsed as ChatStreamTokenEvent;

    case 'done':
      if (!parsed.completion || typeof parsed.completion.status !== 'string' || !parsed.completion.status.trim()) {
        throw new Error('done event must include completion.status.');
      }
      return parsed as ChatStreamDoneEvent;

    default:
      throw new Error(`Unsupported chat stream event type: ${String(parsed?.type ?? '')}`);
  }
}

export function extractChatStreamEvents(buffer: string): ChatStreamParseResult {
  const normalized = buffer.replace(/\r\n/g, '\n');
  const frames = normalized.split('\n\n');
  const remaining = frames.pop() ?? '';
  const events: ChatStreamEvent[] = [];
  const errors: string[] = [];

  for (const frame of frames) {
    const payload = frame
      .split('\n')
      .map((line) => line.trim())
      .filter((line) => line.startsWith('data:'))
      .map((line) => line.slice(5).trim())
      .join('\n');

    if (!payload) continue;

    try {
      events.push(parseChatStreamPayload(payload));
    } catch (error) {
      errors.push((error as Error).message);
    }
  }

  return { events, remaining, errors };
}

export function flushChatStreamBuffer(buffer: string): ChatStreamParseResult {
  const trimmed = buffer.trim();
  if (!trimmed) {
    return { events: [], remaining: '', errors: [] };
  }

  return extractChatStreamEvents(`${trimmed}\n\n`);
}
