import type { UserRole } from "@/lib/enums";

/** Trang đầu sau khi đăng nhập, theo vai trò. */
export function homePathFor(role: UserRole): string {
  switch (role) {
    case "Owner":
    case "Member":
      return "/";
    case "Worker":
      return "/tasks";
    case "SystemAdmin":
      return "/admin";
  }
}
