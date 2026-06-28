import { Component, ElementRef, signal, viewChild, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

/**
 * Catalog assistant chat UI.
 *
 * The JSX structure and Tailwind layout are adapted from vercel/ai-chatbot
 * (components/chat/{messages,message,greeting,suggested-actions,multimodal-input}.tsx).
 * Only the visual/layout layer was ported — there is no AI SDK, useChat hook,
 * streaming, or database wiring. Replies are produced from local mock data so the
 * design can be previewed in isolation.
 */

interface PartCard {
  code: string;
  name: string;
  price: string;
  stock: string;
}

interface MessagePart {
  type: 'text' | 'part-card';
  text?: string;
  card?: PartCard;
}

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  parts: MessagePart[];
}

@Component({
  selector: 'app-catalog-chat',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './catalog-chat.html',
  styleUrl: './catalog-chat.css',
})
export class CatalogChatComponent implements AfterViewChecked {
  private readonly scrollAnchor = viewChild<ElementRef<HTMLDivElement>>('scrollAnchor');
  private shouldScroll = false;

  readonly messages = signal<ChatMessage[]>([]);
  readonly inputValue = signal('');
  readonly isThinking = signal(false);

  readonly suggestions = [
    'Hangi fren balatası bu modele uyar?',
    'PT-4471 stokta var mı, fiyatı ne?',
    'Yağ filtresi alternatiflerini listele',
    'Bu kataloğa toplu fiyat güncellemesi yap',
  ];

  /** Canned mock replies — no network call, no streaming. */
  private readonly mockReplies: MessagePart[][] = [
    [
      {
        type: 'text',
        text: 'Bu araç modeline uygun ön fren balatasını buldum. Aşağıdaki parça stokta ve hemen sevk edilebilir:',
      },
      {
        type: 'part-card',
        card: {
          code: 'PT-4471',
          name: 'Ön Fren Balatası Takımı — Seramik',
          price: '₺1.249,90',
          stock: '38 adet',
        },
      },
      {
        type: 'text',
        text: 'Aynı kategoride daha ekonomik bir alternatif istersen organik balataları da listeleyebilirim.',
      },
    ],
    [
      {
        type: 'text',
        text: 'İşte talep ettiğin yağ filtresi alternatifleri. Fiyat/performans açısından ilki en dengeli seçenek:',
      },
      {
        type: 'part-card',
        card: {
          code: 'PT-2088',
          name: 'Yağ Filtresi — Standart Seri',
          price: '₺189,00',
          stock: '142 adet',
        },
      },
    ],
  ];

  send(): void {
    const text = this.inputValue().trim();
    if (!text || this.isThinking()) {
      return;
    }

    this.messages.update((list) => [
      ...list,
      { id: crypto.randomUUID(), role: 'user', parts: [{ type: 'text', text }] },
    ]);
    this.inputValue.set('');
    this.shouldScroll = true;
    this.isThinking.set(true);

    // Simulate an assistant turn locally (mock only — no API/streaming).
    const reply = this.mockReplies[this.messages().length % this.mockReplies.length];
    setTimeout(() => {
      this.messages.update((list) => [
        ...list,
        { id: crypto.randomUUID(), role: 'assistant', parts: reply },
      ]);
      this.isThinking.set(false);
      this.shouldScroll = true;
    }, 900);
  }

  useSuggestion(text: string): void {
    this.inputValue.set(text);
    this.send();
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  ngAfterViewChecked(): void {
    if (this.shouldScroll) {
      this.scrollAnchor()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'end' });
      this.shouldScroll = false;
    }
  }
}
