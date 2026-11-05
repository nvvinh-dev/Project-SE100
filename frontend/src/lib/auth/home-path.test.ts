import { describe, expect, it } from "vitest";
import { homePathFor } from "@/lib/auth/home-path";

describe("homePathFor", () => {
  it.each([
    ["Owner", "/"],
    ["Member", "/"],
    ["Worker", "/tasks"],
    ["SystemAdmin", "/admin"],
  ] as const)("%s vào %s", (role, path) => {
    expect(homePathFor(role)).toBe(path);
  });
});
