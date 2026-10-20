/** Starting a strategy failed because the user already runs one with that name. */
export class NameTakenError extends Error {
  constructor() {
    super('A running strategy has that name.')
    this.name = 'NameTakenError'
  }
}
