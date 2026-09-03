import createClient from 'openapi-fetch'
import type { components, paths } from './schema'

// D-17 and D-18: every request and response type comes from schema.d.ts, which
// `npm run generate:api` writes from the Swagger the backend publishes. Nothing
// in this folder is typed by hand — that is the whole point of the decision.
export const api = createClient<paths>({
  // Same origin on purpose. In development the Vite proxy forwards /api to the
  // backend; in production the ASP.NET service serves this bundle itself
  // (D-26). Either way the httpOnly cookie of D-20 rides along by itself.
  baseUrl: '/',
  credentials: 'same-origin',
})

export type AuthenticatedUser = components['schemas']['AuthenticatedUser']
export type Role = 'ADMIN' | 'ATTENDANT' | 'MECHANIC'
