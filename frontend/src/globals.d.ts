/** Set from package.json by vite.config.ts. */
declare const __APP_VERSION__: string

interface ImportMetaEnv {
  /** `mock` runs the app against the in-browser mock backend instead of the .NET API. */
  readonly VITE_API?: 'mock'
}
