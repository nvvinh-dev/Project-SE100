import { Loader2Icon, LogOutIcon } from "lucide-react";
import type { ReactNode } from "react";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { PageHeader } from "@/components/common/PageHeader";
import { Button } from "@/components/ui/button";
import { ChangePasswordForm } from "@/features/auth/components/ChangePasswordForm";
import { ProfileForm } from "@/features/auth/components/ProfileForm";
import { useLogout } from "@/features/auth/queries";
import type { CurrentUserResponse } from "@/lib/auth/types";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { userRoleLabels } from "@/lib/enum-labels";

/** `/account`: thông tin cá nhân, đổi mật khẩu, đăng xuất. Mọi vai trò, kể cả Quản trị, dùng được. */
export function AccountPage() {
  const { data: user } = useCurrentUser();

  // AppLayout chỉ dựng trang khi đã có người dùng hiện tại.
  if (!user) {
    return null;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Tài khoản" backTo={backPathFor(user)} />

      <Section title="Thông tin cá nhân">
        <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
          <InfoRow label="Vai trò">{userRoleLabels[user.role]}</InfoRow>
          {user.household && (
            <InfoRow label="Hộ">
              {user.household.name} ({user.household.code})
            </InfoRow>
          )}
        </dl>
        <ProfileForm user={user} />
      </Section>

      <Section title="Đổi mật khẩu" description="Đổi xong, mọi thiết bị đang đăng nhập phải đăng nhập lại.">
        <ChangePasswordForm />
      </Section>

      <LogoutSection />
    </div>
  );
}

function LogoutSection() {
  const logout = useLogout();

  return (
    <Section title="Đăng xuất" description="Chỉ đăng xuất trên thiết bị này.">
      <div className="flex flex-col gap-4">
        {/* Không thu hồi được refresh token (mất mạng) thì ở lại trang: cookie còn hiệu lực, mở lại ứng dụng vẫn vào được. */}
        {logout.isError && <ErrorAlert error={logout.error} />}
        <Button
          type="button"
          variant="outline"
          className="self-start"
          disabled={logout.isPending}
          onClick={() => logout.mutate()}
        >
          {logout.isPending ? <Loader2Icon data-icon="inline-start" className="animate-spin" /> : <LogOutIcon data-icon="inline-start" />}
          Đăng xuất
        </Button>
      </div>
    </Section>
  );
}

// Chủ hộ, Thành viên hộ mở trang từ "Thêm"; Quản trị từ giao diện quản trị; Nhân công mở từ thanh tab nên không có nút quay lại.
function backPathFor(user: CurrentUserResponse): string | undefined {
  switch (user.role) {
    case "Owner":
    case "Member":
      return "/more";
    case "SystemAdmin":
      return "/admin";
    case "Worker":
      return undefined;
  }
}

function Section({ title, description, children }: { title: string; description?: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-4 rounded-lg border p-4">
      <div>
        <h2 className="font-semibold">{title}</h2>
        {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
      </div>
      {children}
    </section>
  );
}

function InfoRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="break-words">{children}</dd>
    </div>
  );
}
