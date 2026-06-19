export const AI_STREAM_SCHEMA_VERSION = 1 as const;

export interface AiStreamFallback {
  used: boolean;
  reason: string | null;
}

export interface AiStreamCompletion {
  status: string;
}

export interface AiStreamSourcesEvent {
  schemaVersion: typeof AI_STREAM_SCHEMA_VERSION;
  type: 'sources';
  sources: any[];
  debugIntent?: unknown;
  fallback: AiStreamFallback;
}

export interface AiStreamTokenEvent {
  schemaVersion: typeof AI_STREAM_SCHEMA_VERSION;
  type: 'token';
  token: string;
  fallback: AiStreamFallback;
}

export interface AiStreamDoneEvent {
  schemaVersion: typeof AI_STREAM_SCHEMA_VERSION;
  type: 'done';
  completion: AiStreamCompletion;
  fallback: AiStreamFallback;
}

export type AiStreamEvent = AiStreamSourcesEvent | AiStreamTokenEvent | AiStreamDoneEvent;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function parseFallback(value: unknown): AiStreamFallback | null {
  if (!isRecord(value) || typeof value['used'] !== 'boolean') {
    return null;
  }

  const reason = value['reason'];
  if (reason !== null && reason !== undefined && typeof reason !== 'string') {
    return null;
  }

  return {
    used: value['used'],
    reason: typeof reason === 'string' ? reason : null,
  };
}

export function parseAiStreamEventPayload(payload: unknown): AiStreamEvent | null {
  if (!isRecord(payload)) return null;
  if (payload['schemaVersion'] !== AI_STREAM_SCHEMA_VERSION) return null;
  if (typeof payload['type'] !== 'string') return null;

  const fallback = parseFallback(payload['fallback']);
  if (!fallback) return null;

  if (payload['type'] === 'sources') {
    if (!Array.isArray(payload['sources'])) return null;
    const debugIntent = payload['debugIntent'] ?? payload['debug_intent'];
    return {
      schemaVersion: AI_STREAM_SCHEMA_VERSION,
      type: 'sources',
      sources: payload['sources'],
      debugIntent,
      fallback,
    };
  }

  if (payload['type'] === 'token') {
    if (typeof payload['token'] !== 'string' || payload['token'].length === 0) return null;
    return {
      schemaVersion: AI_STREAM_SCHEMA_VERSION,
      type: 'token',
      token: payload['token'],
      fallback,
    };
  }

  if (payload['type'] === 'done') {
    const completion = payload['completion'];
    if (!isRecord(completion) || typeof completion['status'] !== 'string' || completion['status'].length === 0) {
      return null;
    }

    return {
      schemaVersion: AI_STREAM_SCHEMA_VERSION,
      type: 'done',
      completion: {
        status: completion['status'],
      },
      fallback,
    };
  }

  return null;
}

export function parseAiStreamSseLine(line: string): AiStreamEvent | null {
  const trimmed = line.trim();
  if (!trimmed.startsWith('data:')) return null;

  try {
    return parseAiStreamEventPayload(JSON.parse(trimmed.slice(5).trim()));
  } catch {
    return null;
  }
}
