import { defineConfig } from "vite";
import { copyFileSync, mkdirSync } from "node:fs";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const defaultOutDir = resolve(__dirname, "./../{name}.Backend/ui");
const outDir = process.env.OUT_DIR ?? defaultOutDir;
const devMode = !!process.env.OUT_DIR;

export default defineConfig({
    plugins: [
        {
            name: "copy-umbraco-manifest",
            writeBundle() {
                if (devMode) mkdirSync(outDir, { recursive: true });
                copyFileSync(
                    resolve(__dirname, "public/umbraco-package.json"),
                    resolve(outDir, "umbraco-package.json")
                );
            },
        },
    ],
    build: {
        lib: {
            entry: "src/index.ts",
            formats: ["es"],
            fileName: "dist",
        },
        outDir,
        emptyOutDir: !devMode,
        sourcemap: true,
        rollupOptions: {
            external: [/^@umbraco/],
        },
    },
});
