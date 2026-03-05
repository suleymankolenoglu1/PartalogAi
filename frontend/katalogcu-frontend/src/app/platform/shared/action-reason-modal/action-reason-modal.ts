import { CommonModule } from '@angular/common';
import { Component, EventEmitter, HostListener, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-action-reason-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './action-reason-modal.html',
  styleUrl: './action-reason-modal.css'
})
export class ActionReasonModalComponent implements OnChanges {
  @Input() open = false;
  @Input() title = 'İşlem Notu';
  @Input() description = '';
  @Input() confirmText = 'Onayla';
  @Input() cancelText = 'Vazgeç';
  @Input() placeholder = 'İşlem notu yazın';
  @Input() required = false;
  @Input() maxLength = 300;
  @Input() pending = false;
  @Input() initialValue: string | null = null;
  @Input() templates: string[] = [];

  @Output() confirm = new EventEmitter<string | null>();
  @Output() closed = new EventEmitter<void>();

  reasonText = '';
  validationError = '';

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['open']?.currentValue) {
      this.reasonText = this.initialValue ?? '';
      this.validationError = '';
    }
  }

  @HostListener('document:keydown.escape')
  onEsc() {
    if (this.open && !this.pending) {
      this.close();
    }
  }

  close() {
    this.closed.emit();
  }

  submit() {
    const trimmed = this.reasonText.trim();
    if (this.required && !trimmed) {
      this.validationError = 'Bu işlem için not zorunlu.';
      return;
    }

    if (trimmed.length > this.maxLength) {
      this.validationError = `Not en fazla ${this.maxLength} karakter olabilir.`;
      return;
    }

    this.validationError = '';
    this.confirm.emit(trimmed.length ? trimmed : null);
  }

  applyTemplate(template: string) {
    if (this.pending) return;
    this.reasonText = template;
    this.validationError = '';
  }
}
