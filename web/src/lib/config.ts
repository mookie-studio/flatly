/** Build-time settings. `VITE_API_URL` is the API origin without a trailing slash. */
export const config = {
  apiUrl: import.meta.env.VITE_API_URL || 'http://localhost:5080',
}
