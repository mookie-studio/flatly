/** The link a new member opens to join a flat. It lands on the join page, which joins after sign-in. */
export function buildInviteLink(origin: string, code: string): string {
  const url = new URL('/join', origin)
  url.searchParams.set('code', code)
  return url.toString()
}

/** A wa.me link with the message filled in, so the member only has to pick a contact. */
export function whatsappShareHref(message: string): string {
  return `https://wa.me/?text=${encodeURIComponent(message)}`
}
