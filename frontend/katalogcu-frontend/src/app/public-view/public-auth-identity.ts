export interface PublicAuthIdentity {
  phone?: string;
  email?: string;
}

export function parsePublicAuthIdentity(value: string): PublicAuthIdentity {
  const trimmed = value.trim();
  if (!trimmed) return {};

  if (trimmed.includes('@')) {
    return { email: trimmed.toLowerCase() };
  }

  const digits = trimmed.replace(/\D/g, '');
  return digits ? { phone: digits } : {};
}

export function formatPublicAuthError(error: unknown, fallback: string): string {
  const payload = (error as { error?: unknown })?.error;
  const code = typeof payload === 'object' && payload !== null && 'code' in payload
    ? String((payload as { code?: unknown }).code ?? '')
    : '';
  const message = typeof payload === 'object' && payload !== null && 'message' in payload
    ? String((payload as { message?: unknown }).message ?? '')
    : (typeof payload === 'string' ? payload : '');

  switch (code) {
    case 'not_found':
      return 'Bu telefon veya e-posta için davet bulunamadı. Bilgileriniz panelde tanımlı değilse işletme ile iletişime geçin.';
    case 'no_password':
      return 'Hesabınız henüz tamamlanmamış. "Hesabı Tamamla" sekmesinden şifrenizi oluşturun.';
    case 'conflict':
      return 'Bu telefon veya e-posta ile hesap zaten tamamlanmış. "Giriş" sekmesinden devam edin.';
    case 'inactive':
      return 'Bu portal erişimi pasif durumda. İşletme ile iletişime geçin.';
    case 'locked':
    case 'invalid_credentials':
      return message || fallback;
    default:
      return message || fallback;
  }
}
