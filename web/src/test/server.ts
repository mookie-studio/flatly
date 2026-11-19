import { setupServer } from 'msw/node'

/** Mocked API for tests. Each test declares the responses it needs. */
export const server = setupServer()
