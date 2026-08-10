import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const dirname = path.dirname(fileURLToPath(import.meta.url))

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      // @brasa/ui (WEB-02) -- source, not a built/published package, so
      // this resolves straight to TS/TSX and Vite transforms it exactly
      // like the rest of this app's own src/. Kept as a path alias rather
      // than an npm workspace: no separate install/build step, no risk of
      // Vite's dependency pre-bundling (which expects compiled JS) trying
      // to swallow raw TypeScript from a "dependency."
      '@brasa/ui': path.resolve(dirname, '../ui/src'),
    },
  },
})
