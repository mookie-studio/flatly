import { createBrowserRouter, createMemoryRouter, type RouteObject } from 'react-router'
import { RequireAuth } from '@/components/layout/Guards'
import { AppLayout } from '@/components/layout/AppLayout'
import { PublicLayout } from '@/components/layout/PublicLayout'
import { Home } from '@/pages/Home'
import { Login } from '@/pages/Login'
import { NotFound } from '@/pages/NotFound'

export const routes: RouteObject[] = [
  {
    element: <PublicLayout />,
    children: [{ path: '/login', element: <Login /> }],
  },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { index: true, element: <Home /> },
          { path: '*', element: <NotFound /> },
        ],
      },
    ],
  },
]

export const createRouter = () => createBrowserRouter(routes)
export const createTestRouter = (path: string) =>
  createMemoryRouter(routes, { initialEntries: [path] })
