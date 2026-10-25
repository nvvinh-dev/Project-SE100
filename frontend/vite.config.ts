/// <reference types="vitest/config" />
import { fileURLToPath, URL } from "node:url";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) },
  },
  server: {
    // Frontend gọi /api cùng địa chỉ với trang để cookie refresh token gửi được, không cần CORS (D-13).
    proxy: { "/api": "http://localhost:5080" },
  },
  test: {
    // Chỉ test hàm thuần trong lib/, không cần môi trường trình duyệt (D-48).
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
});
