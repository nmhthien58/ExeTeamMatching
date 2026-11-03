import '@testing-library/jest-dom/vitest'

// Node 22+ undici Request constructor strictly validates that init.signal is an internal Node/undici AbortSignal.
// JSDOM provides its own AbortSignal implementation on window, which fails undici's brand check.
// If init.signal fails the native check, omit it to avoid TypeError in client-side router navigation.
const NativeRequest = globalThis.Request
if (NativeRequest) {
  globalThis.Request = class Request extends NativeRequest {
    constructor(input: RequestInfo | URL, init?: RequestInit) {
      if (init?.signal) {
        try {
          new NativeRequest('http://localhost', { signal: init.signal })
        } catch {
          const rest = { ...init }
          delete rest.signal
          super(input, rest)
          return
        }
      }
      super(input, init)
    }
  }
}

