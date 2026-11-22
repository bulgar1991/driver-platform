import {
  DriverContractType,
  DriverDocumentType,
  DriverNotify,
} from '@/core/models'

/** Contract types for the staff card, the attendance calendar and its statistics. */
export const CONTRACT_TYPES: { value: DriverContractType; label: string }[] = [
  { value: 'Employee', label: $localize`Permanent employee` },
  { value: 'FixedTerm', label: $localize`Fixed-term` },
  { value: 'Freelance', label: $localize`Freelance` },
  { value: 'Agency', label: $localize`Agency / rental` },
]

export const CONTRACT_LABEL = Object.fromEntries(
  CONTRACT_TYPES.map(t => [t.value, t.label])
) as Record<DriverContractType, string>

export const NOTIFY_OPTIONS: { value: DriverNotify; label: string }[] = [
  { value: 'WhatsAppAndEmail', label: $localize`WhatsApp and email` },
  { value: 'WhatsApp', label: $localize`WhatsApp only` },
  { value: 'Email', label: $localize`Email only` },
]

/** A driver's documents, for the staff card and the expirations page. */
export const DOCUMENT_TYPES: { value: DriverDocumentType; label: string }[] = [
  { value: 'DrivingLicence', label: $localize`Driving licence` },
  { value: 'NccLicence', label: $localize`NCC licence` },
  { value: 'TaxiLicence', label: $localize`Taxi licence` },
  {
    value: 'ProfessionalCertificate',
    label: $localize`Professional certificate (CAP/KB)`,
  },
  { value: 'Insurance', label: $localize`Insurance` },
  { value: 'CriminalRecord', label: $localize`Criminal record` },
]

export const DOCUMENT_LABEL = Object.fromEntries(
  DOCUMENT_TYPES.map(t => [t.value, t.label])
) as Record<DriverDocumentType, string>
