import { defineConfig } from 'vite';
import * as path from 'node:path';

const outDir = path.resolve(__dirname, 'dist');

export default defineConfig({
  base: "./",
  build: {
    outDir,
  }
});
