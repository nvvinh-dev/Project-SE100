// Access token chỉ nằm trong bộ nhớ, không ghi localStorage, sessionStorage (D-12).
// Tải lại trang thì mất, frontend làm mới phiên bằng refresh token trong cookie HttpOnly.
let accessToken: string | null = null;

export function getAccessToken(): string | null {
  return accessToken;
}

export function setAccessToken(token: string | null): void {
  accessToken = token;
}
