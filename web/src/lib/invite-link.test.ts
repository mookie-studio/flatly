import { buildInviteLink, whatsappShareHref } from './invite-link'

describe('invite links', () => {
  it('builds the join link from the web origin and the code', () => {
    expect(buildInviteLink('https://flatly.example', 'ABCD2345')).toBe(
      'https://flatly.example/join?code=ABCD2345',
    )
  })

  it('encodes the message for a WhatsApp share link', () => {
    expect(whatsappShareHref('Únete: https://x.test/join?code=A B')).toBe(
      `https://wa.me/?text=${encodeURIComponent('Únete: https://x.test/join?code=A B')}`,
    )
  })
})
