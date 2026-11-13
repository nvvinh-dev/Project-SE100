import { z } from "zod";
import { memberRoles } from "@/features/households/types";
import { normalizePhone, PHONE_INVALID_MESSAGE } from "@/lib/phone";

// Kiểm tra sớm trên form để báo lỗi ngay dưới ô; backend vẫn kiểm tra lại. Giới hạn theo cột trong database.

// BR-USR-01: báo sớm số không chuẩn hóa được.
const phoneSchema = z
  .string()
  .trim()
  .min(1, { error: "Vui lòng nhập số điện thoại.", abort: true })
  .refine((phone) => normalizePhone(phone) !== null, { error: PHONE_INVALID_MESSAGE });

const fullNameSchema = z
  .string()
  .trim()
  .min(1, { error: "Vui lòng nhập họ tên.", abort: true })
  .max(200, { error: "Họ tên tối đa 200 ký tự." });

// D-22: tối thiểu 8 ký tự, có ít nhất 1 chữ cái và 1 chữ số.
function passwordSchema(emptyMessage: string) {
  return z
    .string()
    .min(1, { error: emptyMessage, abort: true })
    .min(8, { error: "Mật khẩu phải có ít nhất 8 ký tự.", abort: true })
    .regex(/\p{L}/u, { error: "Mật khẩu phải có ít nhất 1 chữ cái." })
    .regex(/[0-9]/, { error: "Mật khẩu phải có ít nhất 1 chữ số." });
}

// Ô chọn giữ chuỗi ("" khi chưa chọn); kiểm tra thuộc tập giá trị rồi thu hẹp kiểu ở đầu ra.
const memberRoleSchema = z
  .string()
  .refine((value): value is (typeof memberRoles)[number] => (memberRoles as readonly string[]).includes(value), {
    error: "Vui lòng chọn vai trò.",
  });

export const householdFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, { error: "Vui lòng nhập tên hộ.", abort: true })
    .max(200, { error: "Tên hộ tối đa 200 ký tự." }),
  productionAddress: z.string().trim().min(1, { error: "Vui lòng nhập địa chỉ khu sản xuất." }),
  contactInfo: z.string().trim().max(500, { error: "Thông tin liên hệ tối đa 500 ký tự." }),
});

export type HouseholdFormValues = z.infer<typeof householdFormSchema>;

export const settingsFormSchema = z.object({
  allowNegativeStock: z.boolean(),
  allowOverlappingSeasons: z.boolean(),
});

export type SettingsFormValues = z.infer<typeof settingsFormSchema>;

export const createMemberFormSchema = z.object({
  fullName: fullNameSchema,
  phone: phoneSchema,
  role: memberRoleSchema,
  initialPassword: passwordSchema("Vui lòng nhập mật khẩu ban đầu."),
});

/** Giá trị trong ô nhập (vai trò là chuỗi, "" khi chưa chọn). */
export type CreateMemberFormValues = z.input<typeof createMemberFormSchema>;
/** Giá trị sau khi kiểm tra, vai trò đã đúng kiểu. */
export type CreateMemberFormOutput = z.output<typeof createMemberFormSchema>;

export const memberInfoFormSchema = z.object({
  fullName: fullNameSchema,
  phone: phoneSchema,
});

export type MemberInfoFormValues = z.infer<typeof memberInfoFormSchema>;

export const resetPasswordFormSchema = z.object({
  newPassword: passwordSchema("Vui lòng nhập mật khẩu mới."),
});

export type ResetPasswordFormValues = z.infer<typeof resetPasswordFormSchema>;
