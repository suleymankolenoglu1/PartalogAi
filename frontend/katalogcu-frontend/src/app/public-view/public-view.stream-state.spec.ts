import {
  initialPublicViewStreamState,
  reducePublicViewStreamState,
} from './public-view.stream-state';

describe('reducePublicViewStreamState', () => {
  it('transitions from idle to connecting on start', () => {
    const next = reducePublicViewStreamState(initialPublicViewStreamState, { type: 'start' });

    expect(next.phase).toBe('connecting');
    expect(next.replySuggestion).toBe('');
    expect(next.products).toEqual([]);
    expect(next.errorMessage).toBeNull();
  });

  it('transitions from connecting to streaming when sources arrive', () => {
    const connecting = reducePublicViewStreamState(initialPublicViewStreamState, { type: 'start' });
    const products = [{ code: 'ABC-123' }];

    const next = reducePublicViewStreamState(connecting, {
      type: 'sourcesReceived',
      products,
    });

    expect(next.phase).toBe('streaming');
    expect(next.products).toEqual(products);
    expect(next.replySuggestion).toBe('');
  });

  it('stays in streaming and appends text on tokenReceived', () => {
    const streaming = reducePublicViewStreamState(
      reducePublicViewStreamState(initialPublicViewStreamState, { type: 'start' }),
      { type: 'sourcesReceived', products: [] }
    );

    const next = reducePublicViewStreamState(streaming, {
      type: 'tokenReceived',
      token: 'Merhaba',
    });

    expect(next.phase).toBe('streaming');
    expect(next.replySuggestion).toBe('Merhaba');
  });

  it('transitions from streaming to completed on complete', () => {
    const streaming = reducePublicViewStreamState(
      reducePublicViewStreamState(
        reducePublicViewStreamState(initialPublicViewStreamState, { type: 'start' }),
        { type: 'sourcesReceived', products: [] }
      ),
      {
        type: 'tokenReceived',
        token: 'Tamam',
      }
    );

    const next = reducePublicViewStreamState(streaming, { type: 'complete' });

    expect(next.phase).toBe('completed');
    expect(next.replySuggestion).toBe('Tamam');
  });

  it('transitions to error on fail', () => {
    const connecting = reducePublicViewStreamState(initialPublicViewStreamState, { type: 'start' });

    const next = reducePublicViewStreamState(connecting, {
      type: 'fail',
      message: 'Baglanti hatasi',
    });

    expect(next.phase).toBe('error');
    expect(next.replySuggestion).toBe('Baglanti hatasi');
    expect(next.errorMessage).toBe('Baglanti hatasi');
  });

  it('transitions back to idle on reset', () => {
    const completed = reducePublicViewStreamState(initialPublicViewStreamState, {
      type: 'restore',
      replySuggestion: 'Hazir',
      products: [{ code: 'SKU-1' }],
    });

    const next = reducePublicViewStreamState(completed, { type: 'reset' });

    expect(next).toEqual(initialPublicViewStreamState);
  });

  it('preserves partial content when an active stream is cancelled', () => {
    const streaming = reducePublicViewStreamState(
      reducePublicViewStreamState(
        reducePublicViewStreamState(initialPublicViewStreamState, { type: 'start' }),
        { type: 'sourcesReceived', products: [] }
      ),
      { type: 'tokenReceived', token: 'Kismi yanit' }
    );

    const cancelled = reducePublicViewStreamState(streaming, { type: 'cancel' });

    expect(cancelled.phase).toBe('cancelled');
    expect(cancelled.replySuggestion).toBe('Kismi yanit');
    expect(cancelled.errorMessage).toBeNull();
  });

  it('ignores late events after cancellation or completion', () => {
    const streaming = reducePublicViewStreamState(
      reducePublicViewStreamState(
        reducePublicViewStreamState(initialPublicViewStreamState, { type: 'start' }),
        { type: 'sourcesReceived', products: [] }
      ),
      { type: 'tokenReceived', token: 'Once' }
    );
    const cancelled = reducePublicViewStreamState(streaming, { type: 'cancel' });
    const lateAfterCancel = reducePublicViewStreamState(cancelled, {
      type: 'tokenReceived',
      token: ' sonra',
    });
    const completed = reducePublicViewStreamState(streaming, { type: 'complete' });
    const lateAfterDone = reducePublicViewStreamState(completed, {
      type: 'sourcesReceived',
      products: [{ code: 'LATE' }],
    });

    expect(lateAfterCancel).toBe(cancelled);
    expect(lateAfterDone).toBe(completed);
  });

  it('starts a clean stream after cancellation', () => {
    const cancelled = reducePublicViewStreamState(
      reducePublicViewStreamState(initialPublicViewStreamState, { type: 'start' }),
      { type: 'cancel' }
    );

    const restarted = reducePublicViewStreamState(cancelled, { type: 'start' });

    expect(restarted).toEqual({
      phase: 'connecting',
      replySuggestion: '',
      products: [],
      errorMessage: null,
    });
  });

  it('ignores token before sources and duplicate sources', () => {
    const connecting = reducePublicViewStreamState(initialPublicViewStreamState, { type: 'start' });
    const earlyToken = reducePublicViewStreamState(connecting, {
      type: 'tokenReceived',
      token: 'erken',
    });
    const streaming = reducePublicViewStreamState(connecting, {
      type: 'sourcesReceived',
      products: [{ code: 'FIRST' }],
    });
    const duplicateSources = reducePublicViewStreamState(streaming, {
      type: 'sourcesReceived',
      products: [{ code: 'SECOND' }],
    });

    expect(earlyToken).toBe(connecting);
    expect(duplicateSources).toBe(streaming);
    expect(duplicateSources.products).toEqual([{ code: 'FIRST' }]);
  });
});
