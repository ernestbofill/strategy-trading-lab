/** Starting a strategy failed because the user already runs one with that name. */
export class NameTakenError extends Error {
  constructor() {
    super('A running strategy has that name.')
    this.name = 'NameTakenError'
  }
}

/** Not logged in (the app), or the admin page needs its password (again), or a wrong email and password. */
export class UnauthorizedError extends Error {
  constructor() {
    super('Not logged in.')
    this.name = 'UnauthorizedError'
  }
}

/** Too many log-in tries; wait and try again. */
export class TooManyTriesError extends Error {
  constructor() {
    super('Too many tries.')
    this.name = 'TooManyTriesError'
  }
}

/** The server refused a value; the message says why, for the user. */
export class InvalidValueError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'InvalidValueError'
  }
}

/** The server refused some fields: one message per field, for the user (`{ email: "Use an email address…" }`). */
export class InvalidFieldsError extends Error {
  readonly fields: Record<string, string>
  constructor(fields: Record<string, string>) {
    super(Object.values(fields)[0] ?? 'That was refused.')
    this.name = 'InvalidFieldsError'
    this.fields = fields
  }
}

/** A verify or reset link that is expired, used or unknown. */
export class ExpiredLinkError extends Error {
  constructor() {
    super('This link has expired.')
    this.name = 'ExpiredLinkError'
  }
}
