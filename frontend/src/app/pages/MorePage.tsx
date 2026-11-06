import { ChevronRightIcon } from "lucide-react";
import { Link } from "react-router";
import { moreGroupsFor } from "@/app/navigation";
import { PageHeader } from "@/components/common/PageHeader";
import { useCurrentUser } from "@/lib/auth/use-current-user";

/** Trang "Thêm" (`/more`): các nhóm chức năng không có trên thanh tab. Mục không có quyền thì ẩn, nhóm rỗng thì ẩn. */
export function MorePage() {
  const { data: user } = useCurrentUser();
  if (!user) {
    return null;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Thêm" />
      {moreGroupsFor(user).map((group) => (
        <section key={group.title} className="flex flex-col gap-2">
          <h2 className="text-sm font-medium text-muted-foreground">{group.title}</h2>
          <ul className="divide-y rounded-lg border">
            {group.items.map((item) => (
              <li key={item.to}>
                <Link to={item.to} className="flex min-h-12 items-center gap-3 px-4 hover:bg-muted">
                  <item.icon className="size-5 text-primary" aria-hidden />
                  <span className="flex-1">{item.label}</span>
                  <ChevronRightIcon className="size-4 text-muted-foreground" aria-hidden />
                </Link>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
