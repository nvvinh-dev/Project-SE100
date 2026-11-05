// Tên khóa dùng chung cho mọi tab của ứng dụng trên cùng trình duyệt.
const REFRESH_LOCK_NAME = "auth-refresh";

/** Phần của Web Locks API (`navigator.locks`) mà việc làm mới phiên cần; tách ra để test thay được. */
export interface RefreshLocks {
  request<T>(name: string, callback: () => Promise<T>): Promise<T>;
}

/**
 * Chạy việc làm mới phiên khi không tab nào khác đang làm mới (BR-USR-05).
 *
 * Các tab dùng chung một cookie refresh token. Hai tab gửi cùng một token một lúc thì yêu cầu sau gửi token vừa bị
 * thay, backend coi là token bị đánh cắp và thu hồi mọi phiên. Chạy lần lượt thì tab sau gửi cookie mới mà tab trước
 * vừa nhận, nên vẫn làm mới được.
 */
export function withRefreshLock<T>(
  task: () => Promise<T>,
  locks: RefreshLocks | null = browserLocks(),
): Promise<T> {
  return locks ? locks.request(REFRESH_LOCK_NAME, task) : task();
}

// navigator.locks chỉ có ở ngữ cảnh an toàn (HTTPS, localhost). Demo trên điện thoại qua HTTP (D-14) thì không có:
// khi đó vẫn làm mới phiên bình thường, chỉ không chặn được hai tab làm mới cùng lúc.
// Trả null, không phải undefined: truyền undefined vào withRefreshLock sẽ rơi vào giá trị mặc định, không tắt được khóa.
function browserLocks(): RefreshLocks | null {
  return typeof navigator === "undefined" ? null : ((navigator.locks as LockManager | undefined) ?? null);
}
