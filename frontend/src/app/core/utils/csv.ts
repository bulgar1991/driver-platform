/** Semicolon-separated with a BOM, so Excel opens it with the right columns and accents. */
export function downloadCsv(
  fileName: string,
  rows: (string | number)[][]
): void {
  const cell = (value: string | number) => {
    const text = String(value)
    return /[";\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
  }
  const csv = '﻿' + rows.map(r => r.map(cell).join(';')).join('\r\n')
  const url = URL.createObjectURL(
    new Blob([csv], { type: 'text/csv;charset=utf-8' })
  )
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}
