import { RouterProvider } from 'react-router/dom'
import './i18n'
import { createRouter } from './router'

const router = createRouter()

export function App() {
  return <RouterProvider router={router} />
}
