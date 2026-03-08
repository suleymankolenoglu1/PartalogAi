export type EmbedEventName = 'part:viewed' | 'cart:add' | 'checkout:start' | 'embed:resize';

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


export function emitEmbedResize(height: number, reason = 'content'): void {
  if (typeof window === 'undefined' || !isEmbedRuntime()) return;
  if (!window.parent || window.parent === window) return;

  const safeHeight = Math.max(320, Math.round(height || 0));
  const envelope: EmbedEventEnvelope = {
    source: 'partalog-embed-client',
    event: 'embed:resize',
    payload: { height: safeHeight, reason },
    timestamp: new Date().toISOString()
  };

  window.parent.postMessage(envelope, '*');
}

export function startEmbedAutoResize(reason = 'content'): () => void {
  if (typeof window === 'undefined' || typeof document === 'undefined' || !isEmbedRuntime()) {
    return () => {};
  }

  let frame: number | null = null;
  const root = document.documentElement;
  const body = document.body;

  const emit = (why = reason) => {
    const height = Math.max(
      root?.scrollHeight || 0,
      root?.offsetHeight || 0,
      body?.scrollHeight || 0,
      body?.offsetHeight || 0,
      window.innerHeight || 0
    );
    emitEmbedResize(height, why);
  };

  const schedule = (why = reason) => {
    if (frame !== null) cancelAnimationFrame(frame);
    frame = requestAnimationFrame(() => {
      frame = null;
      emit(why);
    });
  };

  schedule('init');
  const resizeHandler = () => schedule('window');
  window.addEventListener('resize', resizeHandler);
  window.addEventListener('load', resizeHandler);

  const resizeObserver = typeof ResizeObserver !== 'undefined'
    ? new ResizeObserver(() => schedule('resize-observer'))
    : null;
  resizeObserver?.observe(root);
  if (body) resizeObserver?.observe(body);

  const mutationObserver = typeof MutationObserver !== 'undefined'
    ? new MutationObserver(() => schedule('mutation'))
    : null;
  mutationObserver?.observe(body || root, { childList: true, subtree: true, attributes: true, characterData: true });

  return () => {
    if (frame !== null) cancelAnimationFrame(frame);
    window.removeEventListener('resize', resizeHandler);
    window.removeEventListener('load', resizeHandler);
    resizeObserver?.disconnect();
    mutationObserver?.disconnect();
  };
}
