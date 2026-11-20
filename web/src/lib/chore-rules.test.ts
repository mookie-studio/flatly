import { describe, expect, it } from 'vitest'
import { toChorePayload, validateChore, type ChoreValues } from './chore-rules'

const valid: ChoreValues = {
  name: 'Bins',
  recurrenceUnit: 'Week',
  recurrenceEvery: '1',
  weight: '2',
  participantIds: ['a'],
}

describe('validateChore', () => {
  it('accepts values the API accepts', () => {
    expect(validateChore(valid)).toEqual({})
  })

  it('requires a name once trimmed and caps it at 80 characters', () => {
    expect(validateChore({ ...valid, name: '   ' })).toEqual({ name: 'nameRequired' })
    expect(validateChore({ ...valid, name: 'x'.repeat(81) })).toEqual({ name: 'nameTooLong' })
    expect(validateChore({ ...valid, name: 'x'.repeat(80) })).toEqual({})
  })

  it('keeps the interval a whole number from 1 to 365', () => {
    for (const bad of ['', '0', '366', '1.5', 'abc']) {
      expect(validateChore({ ...valid, recurrenceEvery: bad })).toEqual({
        recurrenceEvery: 'everyRange',
      })
    }
    expect(validateChore({ ...valid, recurrenceEvery: '365' })).toEqual({})
  })

  it('keeps the weight from 1 to 5', () => {
    expect(validateChore({ ...valid, weight: '0' })).toEqual({ weight: 'weightRange' })
    expect(validateChore({ ...valid, weight: '6' })).toEqual({ weight: 'weightRange' })
    expect(validateChore({ ...valid, weight: '5' })).toEqual({})
  })

  it('needs at least one participant', () => {
    expect(validateChore({ ...valid, participantIds: [] })).toEqual({
      participantIds: 'participantsRequired',
    })
  })
})

describe('toChorePayload', () => {
  it('trims the name and sends numbers, not text', () => {
    expect(
      toChorePayload({ ...valid, name: '  Bins  ', recurrenceEvery: '2', weight: '3' }),
    ).toEqual({
      name: 'Bins',
      recurrenceUnit: 'Week',
      recurrenceEvery: 2,
      weight: 3,
      participantIds: ['a'],
    })
  })
})
