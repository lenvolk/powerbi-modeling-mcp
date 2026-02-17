import { defineConfig } from "vite";
import { viteSingleFile } from "vite-plugin-singlefile";

export default defineConfig({
  build: {
    outDir: "build",
    emptyOutDir: true,
    target: "esnext",
    rollupOptions: {
      input: "src/mcp-app.html",
    },
  },
  plugins: [viteSingleFile()],
});
