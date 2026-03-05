export type EmbedEventName = 'part:viewed' | 'cart:add' | 'checkout:start';

interface EmbedEventEnvelope {
  source: 'partalog-embed-client';
  event: EmbedEventName;
  payload: Record<string, unknown>;
  timestamp: string;
}

export function isEmbedRuntime(): boolean {
  if (typeof window === 'undefined') return false;
  const embedded = window.self !== window.top;
  const url = new URL(window.location.href);
  return embedded || url.searchParams.get('embed') === '1';
}

export function emitEmbedEvent(event: EmbedEventName, payload: Record<string, unknown>): void {
  if (typeof window === 'undefined' || !isEmbedRuntime()) return;
  if (!window.parent || window.parent === window) return;

  const envelope: EmbedEventEnvelope = {
    source: 'partalog-embed-client',
    event,
    payload,
    timestamp: new Date().toISOString()
  };

  window.parent.postMessage(envelope, '*');
}
