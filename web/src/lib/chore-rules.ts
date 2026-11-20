// Mirrors the rules the API checks (api/.../Chores/Validation/ChoreRules.cs), so the form can
// explain a mistake before a request is sent. The API stays the authority.

export const CHORE_NAME_MAX_LENGTH = 80
export const RECURRENCE_EVERY_MIN = 1
export const RECURRENCE_EVERY_MAX = 365
export const WEIGHT_MIN = 1
export const WEIGHT_MAX = 5

export const RECURRENCE_UNITS = ['Day', 'Week', 'Month'] as const
export type RecurrenceUnit = (typeof RECURRENCE_UNITS)[number]

/** The form keeps numbers as text, so an empty box can be told apart from a zero. */
export interface ChoreValues {
  name: string
  recurrenceUnit: RecurrenceUnit
  recurrenceEvery: string
  weight: string
  participantIds: string[]
}

export type ChoreField = 'name' | 'recurrenceUnit' | 'recurrenceEvery' | 'weight' | 'participantIds'

/** Each failing field maps to a key under `validation` in the `chores` namespace. */
export type ChoreErrors = Partial<Record<ChoreField, string>>

const CHORE_FIELDS: readonly ChoreField[] = [
  'name',
  'recurrenceUnit',
  'recurrenceEvery',
  'weight',
  'participantIds',
]

export const isChoreField = (key: string): key is ChoreField =>
  (CHORE_FIELDS as readonly string[]).includes(key)

function isIntegerInRange(text: string, min: number, max: number): boolean {
  if (text.trim() === '') return false
  const value = Number(text)
  return Number.isInteger(value) && value >= min && value <= max
}

export function validateChore(values: ChoreValues): ChoreErrors {
  const errors: ChoreErrors = {}
  const name = values.name.trim()

  if (name.length === 0) errors.name = 'nameRequired'
  else if (name.length > CHORE_NAME_MAX_LENGTH) errors.name = 'nameTooLong'

  if (!isIntegerInRange(values.recurrenceEvery, RECURRENCE_EVERY_MIN, RECURRENCE_EVERY_MAX)) {
    errors.recurrenceEvery = 'everyRange'
  }

  if (!isIntegerInRange(values.weight, WEIGHT_MIN, WEIGHT_MAX)) errors.weight = 'weightRange'

  if (values.participantIds.length === 0) errors.participantIds = 'participantsRequired'

  return errors
}

/** The body the API expects, once `validateChore` has passed. */
export function toChorePayload(values: ChoreValues) {
  return {
    name: values.name.trim(),
    recurrenceUnit: values.recurrenceUnit,
    recurrenceEvery: Number(values.recurrenceEvery),
    weight: Number(values.weight),
    participantIds: values.participantIds,
  }
}
