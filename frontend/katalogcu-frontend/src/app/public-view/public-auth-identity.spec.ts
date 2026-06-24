import { formatPublicAuthError, parsePublicAuthIdentity } from './public-auth-identity';

describe('public auth identity helpers', () => {
  it('parses email identifiers', () => {
    expect(parsePublicAuthIdentity(' Customer@Example.COM ')).toEqual({
      email: 'customer@example.com',
    });
  });

  it('parses phone identifiers by keeping only digits', () => {
    expect(parsePublicAuthIdentity(' +90 (555) 111 22 33 ')).toEqual({
      phone: '905551112233',
    });
  });

  it('maps no-password errors to the account completion step', () => {
    const message = formatPublicAuthError(
      { error: { code: 'no_password', message: 'raw' } },
      'fallback'
    );

    expect(message).toContain('Hesabı Tamamla');
  });

  it('maps not-found errors to the controlled invite model', () => {
    const message = formatPublicAuthError(
      { error: { code: 'not_found', message: 'raw' } },
      'fallback'
    );

    expect(message).toContain('davet bulunamadı');
    expect(message).toContain('panelde tanımlı');
  });
});
