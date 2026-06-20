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
    const streaming = reducePublicViewStreamState(initialPublicViewStreamState, { type: 'start' });

    const next = reducePublicViewStreamState(streaming, {
      type: 'tokenReceived',
      token: 'Merhaba',
    });

    expect(next.phase).toBe('streaming');
    expect(next.replySuggestion).toBe('Merhaba');
  });

  it('transitions from streaming to completed on complete', () => {
    const streaming = reducePublicViewStreamState(initialPublicViewStreamState, {
      type: 'tokenReceived',
      token: 'Tamam',
    });

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
});
