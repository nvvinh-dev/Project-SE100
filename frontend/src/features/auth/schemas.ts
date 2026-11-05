import { z } from "zod";
import { normalizePhone, PHONE_INVALID_MESSAGE } from "@/lib/phone";

// BR-USR-01: báo sớm số không chuẩn hóa được; backend vẫn kiểm tra lại.
const phoneSchema = z
  .string()
  .trim()
  .min(1, { error: "Vui lòng nhập số điện thoại.", abort: true })
  .refine((phone) => normalizePhone(phone) !== null, { error: PHONE_INVALID_MESSAGE });

export const loginSchema = z.object({
  phone: phoneSchema,
  password: z.string().min(1, { error: "Vui lòng nhập mật khẩu." }),
});

export type LoginFormValues = z.infer<typeof loginSchema>;

export const registerSchema = z.object({
  fullName: z
    .string()
    .trim()
    .min(1, { error: "Vui lòng nhập họ tên." })
    .max(200, { error: "Họ tên tối đa 200 ký tự." }),
  phone: phoneSchema,
  // D-22: tối thiểu 8 ký tự, có ít nhất 1 chữ cái và 1 chữ số.
  password: z
    .string()
    .min(8, { error: "Mật khẩu phải có ít nhất 8 ký tự." })
    .regex(/\p{L}/u, { error: "Mật khẩu phải có ít nhất 1 chữ cái." })
    .regex(/[0-9]/, { error: "Mật khẩu phải có ít nhất 1 chữ số." }),
  householdName: z
    .string()
    .trim()
    .min(1, { error: "Vui lòng nhập tên hộ." })
    .max(200, { error: "Tên hộ tối đa 200 ký tự." }),
  productionAddress: z.string().trim().min(1, { error: "Vui lòng nhập địa chỉ khu sản xuất." }),
  contactInfo: z.string().trim().max(500, { error: "Thông tin liên hệ tối đa 500 ký tự." }),
});

export type RegisterFormValues = z.infer<typeof registerSchema>;
