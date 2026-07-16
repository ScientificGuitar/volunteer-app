import js from "@eslint/js"
import globals from "globals"
import tseslint from "typescript-eslint"
import pluginReact from "eslint-plugin-react"
import reactHooks from "eslint-plugin-react-hooks"
import reactRefresh from "eslint-plugin-react-refresh"
import { defineConfig, globalIgnores } from "eslint/config"
import stylistic from "@stylistic/eslint-plugin"

export default defineConfig([
  globalIgnores(["dist", "src/lib/generated"]),
  {
    files: ["**/*.{js,mjs,cjs,ts,mts,cts,jsx,tsx}"],
    plugins: { js, "@stylistic": stylistic },
    extends: ["js/recommended"],
    languageOptions: { globals: globals.browser },
  },
  tseslint.configs.recommended,
  reactHooks.configs.flat.recommended,
  reactRefresh.configs.vite,
  pluginReact.configs.flat.recommended,
  pluginReact.configs.flat["jsx-runtime"],
  {
    settings: {
      react: {
        version: "detect",
      },
    },
  },
  {
    rules: {
      "@stylistic/semi": ["error", "never"],
      "@stylistic/indent": ["error", 2],
      "@stylistic/no-multi-spaces": ["error"],
      "@stylistic/no-trailing-spaces": ["error"],
      "@stylistic/block-spacing": ["error", "always"],
      "@stylistic/no-multiple-empty-lines": ["error", { max: 1 }],
      "@stylistic/object-curly-spacing": ["error", "always"],
    },
  },
])
