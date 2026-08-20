export type Service = {
  id: string
  category: string
  title: string
  description: string
  icon: string
  isPolice: boolean
  badge?: string
}

export type ContactInfo = {
  anonymous: boolean
  name?: string
  email?: string
  phone?: string
  preferredMethod?: string
}

export type Draft = {
  location: string
  details: Record<string, unknown>
  contact: ContactInfo
}

export type SubmissionResult = {
  trackingNumber: string
  status: string
  createdAt: string
  disclaimer: string
  reportKind?: string
}

export type AddressValidation = {
  valid: boolean
  normalizedAddress?: string | null
  latitude?: number | null
  longitude?: number | null
  warning?: string | null
}
