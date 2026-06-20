import { AiStreamEvent, parseAiStreamSseLine } from '../core/services/ai-stream-contract';
import {
  initialPublicViewStreamState,
  PublicViewStreamState,
  reducePublicViewStreamState,
} from './public-view.stream-state';

interface ReplayResult {
  events: AiStreamEvent[];
  state: PublicViewStreamState;
  violations: string[];
}

const successReplayLines = [
  'data: {"schemaVersion":1,"type":"sources","sources":[{"id":"p1","code":"ABC-123","name":"Needle plate"}],"fallback":{"used":false,"reason":null}}',
  'data: {"schemaVersion":1,"type":"token","token":"Use ","fallback":{"used":false,"reason":null}}',
  'data: {"schemaVersion":1,"type":"token","token":"ABC-123.","fallback":{"used":false,"reason":null}}',
  'data: {"schemaVersion":1,"type":"done","completion":{"status":"completed"},"fallback":{"used":false,"reason":null}}',
];

const fallbackReplayLines = [
  'data: {"schemaVersion":1,"type":"sources","sources":[],"fallback":{"used":true,"reason":"text_embedding_fallback"}}',
  'data: {"schemaVersion":1,"type":"token","token":"No exact match found.","fallback":{"used":true,"reason":"text_embedding_fallback"}}',
  'data: {"schemaVersion":1,"type":"done","completion":{"status":"completed"},"fallback":{"used":true,"reason":"text_embedding_fallback"}}',
];

function replayAiStreamLines(lines: string[]): ReplayResult {
  let state = reducePublicViewStreamState(initialPublicViewStreamState, { type: 'start' });
  const events: AiStreamEvent[] = [];
  const violations: string[] = [];
  let sawSources = false;
  let sawDone = false;

  for (const line of lines) {
    const event = parseAiStreamSseLine(line);
    if (!event) {
      violations.push('invalid_event');
      continue;
    }

    if (sawDone) {
      violations.push(`event_after_done:${event.type}`);
      continue;
    }

    if (event.type === 'sources') {
      if (sawSources) {
        violations.push('duplicate_sources');
        continue;
      }

      sawSources = true;
      events.push(event);
      state = reducePublicViewStreamState(state, {
        type: 'sourcesReceived',
        products: event.sources,
      });
      continue;
    }

    if (!sawSources) {
      violations.push(`${event.type}_before_sources`);
      continue;
    }

    if (event.type === 'token') {
      events.push(event);
      state = reducePublicViewStreamState(state, {
        type: 'tokenReceived',
        token: event.token,
      });
      continue;
    }

    if (event.type === 'done') {
      sawDone = true;
      events.push(event);
      state = reducePublicViewStreamState(state, { type: 'complete' });
    }
  }

  if (!sawDone) {
    violations.push('missing_done');
  }

  return { events, state, violations };
}

describe('public-view stream replay contract', () => {
  it('replays sources, tokens, and done into completed UI state', () => {
    const replay = replayAiStreamLines(successReplayLines);

    expect(replay.violations).toEqual([]);
    expect(replay.events.map(event => event.type)).toEqual(['sources', 'token', 'token', 'done']);
    expect(replay.state.phase).toBe('completed');
    expect(replay.state.replySuggestion).toBe('Use ABC-123.');
    expect(replay.state.products).toEqual([
      { id: 'p1', code: 'ABC-123', name: 'Needle plate' },
    ]);
  });

  it('preserves fallback metadata across a full replay', () => {
    const replay = replayAiStreamLines(fallbackReplayLines);

    expect(replay.violations).toEqual([]);
    expect(replay.events.every(event => event.fallback.used)).toBeTrue();
    expect(replay.events.map(event => event.fallback.reason)).toEqual([
      'text_embedding_fallback',
      'text_embedding_fallback',
      'text_embedding_fallback',
    ]);
    expect(replay.state.phase).toBe('completed');
    expect(replay.state.replySuggestion).toBe('No exact match found.');
  });

  it('rejects token events before sources and events after done', () => {
    const replay = replayAiStreamLines([
      'data: {"schemaVersion":1,"type":"token","token":"early","fallback":{"used":false,"reason":null}}',
      ...successReplayLines,
      'data: {"schemaVersion":1,"type":"token","token":"late","fallback":{"used":false,"reason":null}}',
    ]);

    expect(replay.violations).toEqual(['token_before_sources', 'event_after_done:token']);
    expect(replay.state.phase).toBe('completed');
    expect(replay.state.replySuggestion).toBe('Use ABC-123.');
  });
});
