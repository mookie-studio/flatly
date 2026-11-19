// Downloads the OpenAPI document of a running API into contracts/flatly-api.json.
// Usage: npm run api:contract -- [api-origin]
// The origin defaults to FLATLY_API_URL, then http://localhost:5080.
import { mkdir, writeFile } from 'node:fs/promises'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const webRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const origin = process.argv[2] ?? process.env.FLATLY_API_URL ?? 'http://localhost:5080'
const source = new URL('/openapi/v1.json', origin)

let response
try {
  response = await fetch(source)
} catch (error) {
  console.error(`Could not reach ${source}. Start the API first (see api/README.md).`)
  console.error(error)
  process.exit(1)
}
if (!response.ok) {
  console.error(`${source} answered ${response.status}. The API must serve its OpenAPI document.`)
  process.exit(1)
}

const document = await response.json()
const target = resolve(webRoot, 'contracts/flatly-api.json')
await mkdir(dirname(target), { recursive: true })
await writeFile(target, `${JSON.stringify(document, null, 2)}\n`)
console.log(`Wrote ${target} from ${source}`)
