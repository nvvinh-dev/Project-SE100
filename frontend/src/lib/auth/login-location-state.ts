/** State điều hướng tới `/login`. */
export interface LoginLocationState {
  /** Trang đang mở khi bị chuyển tới đăng nhập; đăng nhập xong quay lại trang này. */
  from?: string;
  /** Số điện thoại vừa đăng ký, điền sẵn vào ô đăng nhập. */
  phone?: string;
}

/** Đọc state của `/login`; state lạ (người dùng tự gõ đường dẫn) thì bỏ qua. */
export function readLoginLocationState(state: unknown): LoginLocationState {
  if (typeof state !== "object" || state === null) {
    return {};
  }

  const { from, phone } = state as Record<string, unknown>;
  return {
    // Chỉ nhận đường dẫn trong ứng dụng, không để điều hướng ra trang khác.
    from: typeof from === "string" && from.startsWith("/") && !from.startsWith("//") ? from : undefined,
    phone: typeof phone === "string" ? phone : undefined,
  };
}
