const common = new Set(['password1234','password12345','password123456','passwordpassword','qwerty123456','qwertyuiop12','letmein123456','welcome123456','iloveyou12345','admin12345678','סיסמה12345678']);
export const passwordPolicyMessage = 'יש לבחור 12–256 תווים ולפחות 4 תווים שונים. מומלץ משפט סיסמה שאינו נפוץ.';
export function passwordAccepts(password: string): boolean {
  if (password.length < 12 || password.length > 256 || new Set(password.split('')).size < 4) return false;
  const plain = password.normalize('NFKC').toLowerCase().replace(/[\s\p{P}\p{S}]/gu, '');
  if (common.has(plain) || /^(.{1,4})\1+$/.test(plain)) return false;
  return !['012345678901234567890123456789','123456789012345678901234567890','abcdefghijklmnopqrstuvwxyz','qwertyuiopasdfghjklzxcvbnm'].some(sequence => plain.length >= 8 && sequence.includes(plain));
}
