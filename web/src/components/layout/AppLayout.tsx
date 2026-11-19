import { Outlet } from 'react-router'

// Placeholder for the authenticated layout. The app shell (#49) replaces this.
export function AppLayout() {
  return (
    <main>
      <Outlet />
    </main>
  )
}
