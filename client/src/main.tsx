import { StrictMode } from "react"
import { createRoot } from "react-dom/client"

import "./index.css"
import App from "./App.tsx"
import { ThemeProvider } from "@/components/theme-provider.tsx"
import { ClerkProvider } from "@clerk/react"
import { shadcn } from "@clerk/ui/themes"

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <ClerkProvider publishableKey={import.meta.env.VITE_CLERK_PUBLISHABLE_KEY} appearance={{ theme: shadcn }}>
      <ThemeProvider>
        <App />
      </ThemeProvider>
    </ClerkProvider>
  </StrictMode>
)
