const currency = new Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY', minimumFractionDigits: 2 })
const number = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const dateTime = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'short', timeStyle: 'short' })
const date = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium' })

export const MONTHS = ['Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık']

export function money(value: number | null | undefined) {
  return currency.format(value ?? 0)
}

export function num(value: number | null | undefined) {
  return number.format(value ?? 0)
}

export function percent(value: number | null | undefined) {
  return value == null ? '' : `%${number.format(value).replace(/,00$/, '')}`
}

export function formatDateTime(value: string | null | undefined) {
  return value ? dateTime.format(new Date(value)) : ''
}

export function formatDate(value: string | null | undefined) {
  return value ? date.format(new Date(value + (value.length === 10 ? 'T00:00:00' : ''))) : ''
}

export function periodLabel(year: number, month: number) {
  return `${MONTHS[month - 1]} ${year}`
}
