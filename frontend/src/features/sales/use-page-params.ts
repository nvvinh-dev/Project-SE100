import { useSearchParams } from "react-router";

/**
 * Đổi tham số trên đường dẫn của trang danh sách: đổi bộ lọc, từ khóa thì về trang 1; chỉ giữ tham số có giá trị
 * để đường dẫn gọn và quay lại vẫn giữ bộ lọc.
 */
export function usePageParams() {
  const [, setSearchParams] = useSearchParams();

  return (changes: Record<string, string | undefined>) => {
    setSearchParams((params) => {
      const next = new URLSearchParams(params);
      for (const [key, value] of Object.entries(changes)) {
        if (value) {
          next.set(key, value);
        } else {
          next.delete(key);
        }
      }
      if (!("page" in changes)) {
        next.delete("page");
      }
      return next;
    });
  };
}

/** Giá trị lạ trên đường dẫn (người dùng tự sửa) thì bỏ qua thay vì gửi lên API. */
export function pickEnum<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((item) => item === value);
}
