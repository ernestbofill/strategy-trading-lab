/** "AI model you used": common services and models, plus Other with a name of the user's own (spec: AI round). */
export const AI_MODELS = ['ChatGPT · GPT-5', 'Claude · Opus', 'Gemini · 2.5 Pro']

/** The select's value for a model of the user's own; its label is translated. */
export const OTHER_MODEL = 'other'

/** "Open in …": plain links in a new tab. The app doesn't connect to them. */
export const AI_SERVICES = [
  { name: 'ChatGPT', url: 'https://chatgpt.com/' },
  { name: 'Claude', url: 'https://claude.ai/new' },
  { name: 'Gemini', url: 'https://gemini.google.com/app' },
]
