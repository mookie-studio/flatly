type SessionExpiredHandler = () => void

let onSessionExpired: SessionExpiredHandler = () => {}

/** The app registers here what to do when the API answers 401 (for example, go to login). */
export function setSessionExpiredHandler(handler: SessionExpiredHandler) {
  onSessionExpired = handler
}

export function notifySessionExpired() {
  onSessionExpired()
}
