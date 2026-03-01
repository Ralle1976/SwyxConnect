import type { PropsWithChildren } from 'react'

export function MainContent({ children }: PropsWithChildren) {
  return (
    <main className="flex-1 overflow-y-auto bg-zinc-50 dark:bg-zinc-950 p-4 scrollbar-thin scrollbar-thumb-zinc-300 dark:scrollbar-thumb-zinc-700 scrollbar-track-transparent">
      {children}
    </main>
  )
}
