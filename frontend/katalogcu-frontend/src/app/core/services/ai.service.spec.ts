import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { AiStreamEvent } from './ai-stream-contract';
import { AiService } from './ai.service';

const sourcesLine =
  'data: {"schemaVersion":1,"type":"sources","sources":[],"fallback":{"used":false,"reason":null}}\n';
const tokenLine =
  'data: {"schemaVersion":1,"type":"token","token":"yanit","fallback":{"used":false,"reason":null}}\n';
const doneLine =
  'data: {"schemaVersion":1,"type":"done","completion":{"status":"completed"},"fallback":{"used":false,"reason":null}}\n';

function streamResponse(lines: string[]): Response {
  const encoder = new TextEncoder();
  return new Response(
    new ReadableStream<Uint8Array>({
      start(controller) {
        for (const line of lines) controller.enqueue(encoder.encode(line));
        controller.close();
      },
    }),
    { status: 200 }
  );
}

describe('AiService streaming lifecycle', () => {
  let service: AiService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient()] });
    service = TestBed.inject(AiService);
  });

  it('aborts the fetch request when the stream subscription is cancelled', () => {
    const fetchSpy = spyOn(window, 'fetch').and.returnValue(
      new Promise<Response>(() => undefined)
    );

    const subscription = service.sendMessageStream('soru', null, []).subscribe();
    subscription.unsubscribe();
    const signal = fetchSpy.calls.mostRecent().args[1]?.signal as AbortSignal;

    expect(signal.aborted).toBeTrue();
  });

  it('reports a dropped connection when EOF arrives before done', (done) => {
    spyOn(window, 'fetch').and.resolveTo(streamResponse([sourcesLine, tokenLine]));

    service.sendMessageStream('soru', null, []).subscribe({
      error: (error: Error) => {
        expect(error.message).toBe('SSE akisi tamamlanmadan kesildi.');
        done();
      },
      complete: () => done.fail('stream should not complete without done'),
    });
  });

  it('treats done as terminal and ignores later events', (done) => {
    spyOn(window, 'fetch').and.resolveTo(
      streamResponse([sourcesLine, doneLine, tokenLine])
    );
    const events: AiStreamEvent[] = [];

    service.sendMessageStream('soru', null, []).subscribe({
      next: (event) => events.push(event),
      error: (error) => done.fail(error),
      complete: () => {
        expect(events.map((event) => event.type)).toEqual(['sources', 'done']);
        done();
      },
    });
  });

  it('starts a clean stream after a cancelled request', (done) => {
    let callCount = 0;
    const fetchSpy = spyOn(window, 'fetch').and.callFake(() => {
      callCount += 1;
      if (callCount === 1) {
        return new Promise<Response>(() => undefined);
      }
      return Promise.resolve(streamResponse([sourcesLine, tokenLine, doneLine]));
    });

    const first = service.sendMessageStream('ilk', null, []).subscribe();
    first.unsubscribe();
    const events: AiStreamEvent[] = [];

    service.sendMessageStream('ikinci', null, []).subscribe({
      next: (event) => events.push(event),
      error: (error) => done.fail(error),
      complete: () => {
        const firstSignal = fetchSpy.calls.argsFor(0)[1]?.signal as AbortSignal;
        expect(firstSignal.aborted).toBeTrue();
        expect(events.map((event) => event.type)).toEqual(['sources', 'token', 'done']);
        done();
      },
    });
  });
});
