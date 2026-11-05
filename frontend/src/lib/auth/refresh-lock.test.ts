import { describe, expect, it } from "vitest";
import { type RefreshLocks, withRefreshLock } from "@/lib/auth/refresh-lock";

// Khóa giả chạy các việc cùng tên lần lượt như navigator.locks, ghi lại tên khóa được xin.
function fakeLocks(): RefreshLocks & { names: string[] } {
  let tail: Promise<unknown> = Promise.resolve();
  const names: string[] = [];
  return {
    names,
    request<T>(name: string, callback: () => Promise<T>): Promise<T> {
      names.push(name);
      const run = tail.then(callback);
      tail = run.catch(() => undefined);
      return run;
    },
  };
}

function deferred(): { promise: Promise<void>; resolve: () => void } {
  let resolve = () => {};
  const promise = new Promise<void>((r) => {
    resolve = r;
  });
  return { promise, resolve };
}

describe("withRefreshLock", () => {
  it("chạy lần lượt: việc sau chỉ bắt đầu khi việc trước xong", async () => {
    const locks = fakeLocks();
    const log: string[] = [];
    const firstDone = deferred();

    const first = withRefreshLock(async () => {
      log.push("tab 1 bắt đầu");
      await firstDone.promise;
      log.push("tab 1 xong");
      return 1;
    }, locks);
    const second = withRefreshLock(async () => {
      log.push("tab 2 bắt đầu");
      return 2;
    }, locks);

    await Promise.resolve();
    expect(log).toEqual(["tab 1 bắt đầu"]);

    firstDone.resolve();
    expect(await Promise.all([first, second])).toEqual([1, 2]);
    expect(log).toEqual(["tab 1 bắt đầu", "tab 1 xong", "tab 2 bắt đầu"]);
    expect(locks.names).toEqual(["auth-refresh", "auth-refresh"]);
  });

  it("việc trước lỗi thì việc sau vẫn chạy", async () => {
    const locks = fakeLocks();

    const first = withRefreshLock(() => Promise.reject(new Error("mất mạng")), locks);
    const second = withRefreshLock(() => Promise.resolve("ok"), locks);

    await expect(first).rejects.toThrow("mất mạng");
    expect(await second).toBe("ok");
  });

  it("không có navigator.locks thì chạy thẳng", async () => {
    expect(await withRefreshLock(() => Promise.resolve("ok"), null)).toBe("ok");
  });
});
