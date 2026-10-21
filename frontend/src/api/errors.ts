/** Starting a strategy failed because the user already runs one with that name. */
export class NameTakenError extends Error {
  constructor() {
    super('A running strategy has that name.')
    this.name = 'NameTakenError'
  }
}

/** The admin page needs its password (again). */
export class UnauthorizedError extends Error {
  constructor() {
    super('Not logged in to the admin page.')
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
