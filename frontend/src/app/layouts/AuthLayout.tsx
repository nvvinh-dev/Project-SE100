import { Outlet } from "react-router";

/** Khung của trang đăng nhập, đăng ký: một cột hẹp, tên ứng dụng ở trên. */
export function AuthLayout() {
  return (
    <main className="flex min-h-svh flex-col items-center px-4 py-10 md:justify-center md:bg-muted/40">
      <div className="flex w-full max-w-sm flex-col gap-8 md:rounded-xl md:border md:bg-background md:p-8 md:shadow-sm">
        <p className="text-center text-2xl font-semibold text-primary">Sổ Tay Nhà Nông</p>
        <Outlet />
      </div>
    </main>
  );
}
