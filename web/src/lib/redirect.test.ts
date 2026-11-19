import { safeRedirectPath } from './redirect'

describe('safeRedirectPath', () => {
  it.each([
    ['/rooms/1?tab=week#top', '/rooms/1?tab=week#top'],
    ['/', '/'],
  ])('keeps the same-origin path %s', (input, expected) => {
    expect(safeRedirectPath(input)).toBe(expected)
  })

  it.each([
    [null],
    [undefined],
    [''],
    ['rooms'],
    ['https://example.com/'],
    ['//example.com'],
    ['/\\example.com'],
    ['/rooms\n/x'],
  ])('falls back to home for %j', (input) => {
    expect(safeRedirectPath(input)).toBe('/')
  })
})
