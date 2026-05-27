import path from "node:path";
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import electron from "vite-plugin-electron/simple";

export default defineConfig({
  plugins: [
    react(),
    electron({
      main: {
        entry: "src/main/main.ts",
        vite: {
          build: {
            rollupOptions: {
              output: {
                entryFileNames: "main.mjs"
              }
            }
          }
        }
      },
      preload: {
        input: "src/preload/index.ts",
        vite: {
          build: {
            rollupOptions: {
              output: {
                entryFileNames: "preload.mjs"
              }
            }
          }
        }
      }
    })
  ],
  resolve: {
    alias: {
      "@shared": path.resolve(__dirname, "src/shared"),
      "@renderer": path.resolve(__dirname, "src/renderer")
    }
  },
  build: {
    outDir: "dist"
  }
});
