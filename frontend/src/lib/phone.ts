// Chuẩn hóa số điện thoại giống backend (BR-USR-01), để báo lỗi ngay trên form và gửi đi đúng một dạng.

export const PHONE_INVALID_MESSAGE = "Số điện thoại phải có 10 chữ số, bắt đầu bằng 0 hoặc +84.";

/**
 * Bỏ khoảng trắng, dấu chấm, dấu gạch; đổi tiền tố `+84`, `84` thành `0`.
 * Trả về số 10 chữ số bắt đầu bằng `0`, hoặc `null` nếu không hợp lệ.
 */
export function normalizePhone(input: string): string | null {
  let compact = input.replace(/[\s.-]/g, "");

  if (compact.startsWith("+84")) {
    compact = `0${compact.slice(3)}`;
  } else if (compact.startsWith("84") && compact.length === 11) {
    // Chỉ đổi 84 khi chuỗi có 11 chữ số, tức là mã quốc gia đứng trước 9 chữ số còn lại của số.
    compact = `0${compact.slice(2)}`;
  }

  return /^0[0-9]{9}$/.test(compact) ? compact : null;
}
