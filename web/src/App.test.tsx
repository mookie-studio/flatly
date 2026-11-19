import { render, screen } from '@testing-library/react'
import { HttpResponse, http } from 'msw'
import { config } from '@/lib/config'
import { server } from '@/test/server'
import { App } from './App'

describe('App', () => {
  it('renders the app shell', async () => {
    // No session: the shell is the public login page, which also has a main landmark.
    server.use(
      http.get(
        `${config.apiUrl}/api/v1/platform/auth/session`,
        () => new HttpResponse(null, { status: 401 }),
      ),
    )
    render(<App />)
    expect(await screen.findByRole('main')).toBeInTheDocument()
  })
})
