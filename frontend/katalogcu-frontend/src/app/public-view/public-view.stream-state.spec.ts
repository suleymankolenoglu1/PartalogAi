import {
  beginPublicChatStream,
  ChatMessage,
  PublicChatStreamState,
  reducePublicChatStreamState,
} from './public-view.stream-state';

describe('public view stream state', () => {
  const emptyState = (): PublicChatStreamState => ({
    messages: [],
    chatHistory: [],
    latestAssistantMessage: null,
    aiState: {
      isActive: false,
      isLoading: false,
      response: null,
    },
  });

  it('models sources -> token -> done as one explicit lifecycle', () => {
    let state = beginPublicChatStream(emptyState(), 'yağ deposu contası', 'u1', 'a1');

    expect(state.messages.map((message) => message.role)).toEqual(['user', 'assistant']);
    expect(state.aiState.isLoading).toBeTrue();

    state = reducePublicChatStreamState(state, {
      type: 'sources',
      products: [{ code: '4109410', name: 'Conta' }],
    });

    expect(state.aiState.isLoading).toBeFalse();
    expect(state.aiState.response?.products[0].code).toBe('4109410');
    expect(state.aiState.response?.replySuggestion).toBe('');

    state = reducePublicChatStreamState(state, { type: 'token', token: 'Parça ' });
    state = reducePublicChatStreamState(state, { type: 'token', token: '4109410.' });

    expect(state.messages[1].text).toBe('Parça 4109410.');
    expect(state.aiState.response?.replySuggestion).toBe('Parça 4109410.');

    state = reducePublicChatStreamState(state, { type: 'done' });

    expect(state.messages[1].isStreaming).toBeFalse();
    expect(state.aiState.isLoading).toBeFalse();
    expect(state.latestAssistantMessage?.text).toBe('Parça 4109410.');
    expect(state.chatHistory).toEqual([
      { role: 'user', text: 'yağ deposu contası' },
      { role: 'assistant', text: 'Parça 4109410.' },
    ]);
  });

  it('keeps UI consistent when tokens arrive before sources', () => {
    let state = beginPublicChatStream(emptyState(), 'kod nedir', 'u1', 'a1');
    state = reducePublicChatStreamState(state, { type: 'token', token: 'Kod ' });
    state = reducePublicChatStreamState(state, { type: 'sources', products: [{ code: 'A1' }] });
    state = reducePublicChatStreamState(state, { type: 'token', token: 'A1' });

    expect(state.aiState.isLoading).toBeFalse();
    expect(state.messages[1].text).toBe('Kod A1');
    expect(state.aiState.response).toEqual({
      replySuggestion: 'Kod A1',
      products: [{ code: 'A1' }],
    });
  });

  it('ends loading and preserves the failed assistant bubble on stream error', () => {
    let state = beginPublicChatStream(emptyState(), 'merhaba', 'u1', 'a1');
    state = reducePublicChatStreamState(state, {
      type: 'error',
      message: 'Bağlantı hatası, lütfen tekrar deneyin.',
    });

    expect(state.aiState.isLoading).toBeFalse();
    expect(state.aiState.response).toBeNull();
    expect(state.messages[1].isStreaming).toBeFalse();
    expect(state.messages[1].text).toBe('Bağlantı hatası, lütfen tekrar deneyin.');
  });

  it('restores message list, loading flag and replySuggestion from saved history', () => {
    const messages: ChatMessage[] = [
      { role: 'user', text: 'soru', timestamp: 'u1' },
      {
        role: 'assistant',
        text: 'cevap',
        timestamp: 'a1',
        products: [{ code: 'P1' }],
        isStreaming: false,
      },
    ];

    const state = reducePublicChatStreamState(emptyState(), { type: 'restore', messages });

    expect(state.messages).toEqual(messages);
    expect(state.aiState.isActive).toBeTrue();
    expect(state.aiState.isLoading).toBeFalse();
    expect(state.aiState.response?.replySuggestion).toBe('cevap');
    expect(state.aiState.response?.products[0].code).toBe('P1');
  });
});
