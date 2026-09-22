import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { switchMap } from 'rxjs';

export interface OwnerLoginResult { username?: string; setupRequired?: boolean; requiresTwoFactor?: boolean; }
export interface OwnerProof { password: string; code?: string; recoveryCode?: string; }

@Injectable({ providedIn: 'root' })
export class AdminAuth {
  private http = inject(HttpClient);
  session() { return this.http.get<{ username: string; setupRequired?: boolean }>('/api/admin/session'); }
  private write<T>(path: string, body: unknown) {
    return this.http.get<{ token: string }>('/api/admin/csrf').pipe(
      switchMap(({ token }) => this.http.post<T>('/api/admin/' + path, body, { headers: { 'X-CSRF-TOKEN': token } }))
    );
  }
  login(username: string, password: string) { return this.write<OwnerLoginResult>('login', { username, password }); }
  verify(code: string, recovery = false) { return this.write<OwnerLoginResult>('login/verify', recovery ? { recoveryCode: code } : { code }); }
  security() { return this.http.get<{ enabled: boolean; recoveryCodesRemaining: number }>('/api/admin/security'); }
  setup(proof: OwnerProof) { return this.write<{ sharedKey: string; accountName: string; expiresInMinutes: number }>('security/setup', proof); }
  confirm(code: string) { return this.write<{ recoveryCodes: string[] }>('security/confirm', { code }); }
  recoveryCodes(proof: OwnerProof) { return this.write<{ recoveryCodes: string[] }>('security/recovery-codes', proof); }
  logout() { return this.write<void>('logout', {}); }
}
