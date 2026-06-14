export interface AiResponse {
  replySuggestion: string;
  products: any[];
  debugInfo?: string;
  compareGroups?: CompareGroup[];
}

export interface CompareGroup {
  query: string;
  results: any[];
}

export interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  timestamp: string;
  products?: any[];
  compareGroups?: CompareGroup[];
  isStreaming?: boolean;
  feedback?: 'up' | 'down';
  feedbackSubmitted?: boolean;
}

export interface PublicChatStreamState {
  messages: ChatMessage[];
  chatHistory: { role: string; text: string }[];
  aiState: {
    isActive: boolean;
    isLoading: boolean;
    response: null | AiResponse;
  };
  latestAssistantMessage: ChatMessage | null;
}

export type PublicChatStreamEvent =
  | { type: 'sources'; products: any[] }
  | { type: 'token'; token: string }
  | { type: 'done' }
  | { type: 'error'; message: string }
  | { type: 'restore'; messages: ChatMessage[] };

export function beginPublicChatStream(
  state: PublicChatStreamState,
  userText: string,
  userTimestamp: string,
  assistantTimestamp: string
): PublicChatStreamState {
  const userMsg: ChatMessage = { role: 'user', text: userText, timestamp: userTimestamp };
  const assistantMsg: ChatMessage = {
    role: 'assistant',
    text: '',
    timestamp: assistantTimestamp,
    products: [],
    isStreaming: true,
  };

  return {
    ...state,
    messages: [...state.messages, userMsg, assistantMsg],
    chatHistory: [...state.chatHistory, { role: 'user', text: userText }],
    latestAssistantMessage: null,
    aiState: {
      isActive: true,
      isLoading: true,
      response: null,
    },
  };
}

export function reducePublicChatStreamState(
  state: PublicChatStreamState,
  event: PublicChatStreamEvent
): PublicChatStreamState {
  if (event.type === 'restore') {
    return restorePublicChatStreamState(state, event.messages);
  }

  const activeIndex = findActiveAssistantIndex(state.messages);
  if (activeIndex < 0) {
    return state;
  }

  const activeMessage = state.messages[activeIndex];

  if (event.type === 'sources') {
    const nextMessage = {
      ...activeMessage,
      products: event.products,
    };

    return replaceActiveMessage(state, activeIndex, nextMessage, {
      isActive: true,
      isLoading: false,
      response: {
        replySuggestion: nextMessage.text,
        products: event.products,
      },
    });
  }

  if (event.type === 'token') {
    const nextText = `${activeMessage.text}${event.token}`;
    const nextMessage = {
      ...activeMessage,
      text: nextText,
    };

    return replaceActiveMessage(state, activeIndex, nextMessage, {
      isActive: true,
      isLoading: false,
      response: {
        replySuggestion: nextText,
        products: nextMessage.products || [],
      },
    });
  }

  if (event.type === 'done') {
    const nextMessage = {
      ...activeMessage,
      isStreaming: false,
    };

    return {
      ...replaceActiveMessage(state, activeIndex, nextMessage, {
        isActive: true,
        isLoading: false,
        response: {
          replySuggestion: nextMessage.text,
          products: nextMessage.products || [],
        },
      }),
      chatHistory: nextMessage.text
        ? [...state.chatHistory, { role: 'assistant', text: nextMessage.text }]
        : state.chatHistory,
      latestAssistantMessage: nextMessage,
    };
  }

  const nextMessage = {
    ...activeMessage,
    text: event.message,
    isStreaming: false,
  };

  return {
    ...replaceActiveMessage(state, activeIndex, nextMessage, {
      isActive: true,
      isLoading: false,
      response: null,
    }),
    latestAssistantMessage: null,
  };
}

function restorePublicChatStreamState(
  state: PublicChatStreamState,
  messages: ChatMessage[]
): PublicChatStreamState {
  const latestAssistantMessage =
    [...messages].reverse().find((message) => message.role === 'assistant' && !message.isStreaming) ??
    null;

  return {
    ...state,
    messages,
    chatHistory: messages.map((message) => ({ role: message.role, text: message.text })),
    latestAssistantMessage,
    aiState: {
      isActive: messages.length > 0,
      isLoading: messages.some((message) => message.role === 'assistant' && !!message.isStreaming),
      response: latestAssistantMessage
        ? {
            replySuggestion: latestAssistantMessage.text,
            products: latestAssistantMessage.products || [],
            compareGroups: latestAssistantMessage.compareGroups || [],
          }
        : null,
    },
  };
}

function replaceActiveMessage(
  state: PublicChatStreamState,
  index: number,
  message: ChatMessage,
  aiState: PublicChatStreamState['aiState']
): PublicChatStreamState {
  const messages = [...state.messages];
  messages[index] = message;

  return {
    ...state,
    messages,
    aiState,
  };
}

function findActiveAssistantIndex(messages: ChatMessage[]): number {
  for (let index = messages.length - 1; index >= 0; index--) {
    const message = messages[index];
    if (message.role === 'assistant' && message.isStreaming) {
      return index;
    }
  }

  return -1;
}
