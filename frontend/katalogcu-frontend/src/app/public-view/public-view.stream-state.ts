export type PublicViewStreamPhase =
  | 'idle'
  | 'connecting'
  | 'streaming'
  | 'completed'
  | 'cancelled'
  | 'error';

export interface PublicViewStreamState {
  phase: PublicViewStreamPhase;
  replySuggestion: string;
  products: any[];
  errorMessage: string | null;
}

export type PublicViewStreamAction =
  | { type: 'start' }
  | { type: 'sourcesReceived'; products: any[] }
  | { type: 'tokenReceived'; token: string }
  | { type: 'complete' }
  | { type: 'cancel' }
  | { type: 'fail'; message: string }
  | { type: 'restore'; replySuggestion: string; products: any[] }
  | { type: 'reset' };

export const initialPublicViewStreamState: PublicViewStreamState = {
  phase: 'idle',
  replySuggestion: '',
  products: [],
  errorMessage: null,
};

export function reducePublicViewStreamState(
  state: PublicViewStreamState,
  action: PublicViewStreamAction
): PublicViewStreamState {
  switch (action.type) {
    case 'start':
      return {
        phase: 'connecting',
        replySuggestion: '',
        products: [],
        errorMessage: null,
      };

    case 'sourcesReceived':
      if (state.phase !== 'connecting') return state;
      return {
        ...state,
        phase: 'streaming',
        products: action.products,
        errorMessage: null,
      };

    case 'tokenReceived':
      if (state.phase !== 'streaming') return state;
      return {
        ...state,
        phase: 'streaming',
        replySuggestion: `${state.replySuggestion}${action.token}`,
        errorMessage: null,
      };

    case 'complete':
      if (state.phase !== 'streaming') return state;
      return {
        ...state,
        phase: 'completed',
        errorMessage: null,
      };

    case 'cancel':
      if (state.phase !== 'connecting' && state.phase !== 'streaming') return state;
      return {
        ...state,
        phase: 'cancelled',
        errorMessage: null,
      };

    case 'fail':
      if (state.phase !== 'connecting' && state.phase !== 'streaming') return state;
      return {
        ...state,
        phase: 'error',
        errorMessage: action.message,
        replySuggestion: action.message,
      };

    case 'restore':
      return {
        phase: 'completed',
        replySuggestion: action.replySuggestion,
        products: action.products,
        errorMessage: null,
      };

    case 'reset':
      return initialPublicViewStreamState;

    default:
      return state;
  }
}
