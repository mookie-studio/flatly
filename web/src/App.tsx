import { RouterProvider } from 'react-router/dom'
import './i18n'
import { ThemeProvider } from './theme/ThemeProvider'
import { createRouter } from './router'

const router = createRouter()

export function App() {
  return (
    <ThemeProvider>
      <RouterProvider router={router} />
    </ThemeProvider>
  )
}
