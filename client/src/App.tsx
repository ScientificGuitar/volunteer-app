import { Show, SignInButton, SignUpButton, UserButton, useClerk } from "@clerk/react"
import { Moon, Sun } from "lucide-react"

import { Button } from "@/components/ui/button"
import { useTheme } from "@/components/theme-provider"

export function App() {
  const { openSignIn, openSignUp, signOut } = useClerk()
  const { theme, setTheme } = useTheme()

  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex items-center justify-between border-b px-6 py-3">
        <a href="/" className="text-lg font-semibold hover:underline">
          Rosterly
        </a>
        <Show when="signed-in">
          <div className="flex items-center gap-2">
            <UserButton>
              <UserButton.MenuItems>
                <UserButton.Action
                  label={theme === "dark" ? "Light mode" : "Dark mode"}
                  labelIcon={
                    theme === "dark" ? <Sun size={16} /> : <Moon size={16} />
                  }
                  onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
                />
              </UserButton.MenuItems>
            </UserButton>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => signOut({ redirectUrl: "/" })}
            >
              Sign out
            </Button>
          </div>
        </Show>
        <Show when="signed-out">
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              onClick={() => openSignIn({ fallbackRedirectUrl: "/" })}
            >
              Sign in
            </Button>
            <Button onClick={() => openSignUp({ fallbackRedirectUrl: "/" })}>
              Sign up
            </Button>
          </div>
        </Show>
      </header>
      <main className="flex flex-1 items-center justify-center p-6">
        <Show when="signed-out">
          <div className="max-w-md text-center">
            <h1 className="mb-2 text-2xl font-bold">Welcome to Rosterly</h1>
            <p className="text-muted-foreground">
              Sign in to manage your organization&rsquo;s volunteer scheduling.
            </p>
          </div>
        </Show>
        <Show when="signed-in">
          <div className="max-w-md text-center">
            <h1 className="mb-2 text-2xl font-bold">Project ready!</h1>
            <p className="text-muted-foreground">
              You are signed in. You may now add components and start building.
            </p>
          </div>
        </Show>
      </main>
    </div>
  )
}

export default App
