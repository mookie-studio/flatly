import { loginHref } from './router'

describe('loginHref', () => {
  it('keeps path, query and hash so the visitor returns to the same place', () => {
    const href = loginHref({ pathname: '/rooms/42', search: '?tab=week', hash: '#today' })

    expect(href).toBe(`/login?${new URLSearchParams({ redirect: '/rooms/42?tab=week#today' })}`)
  })
})
