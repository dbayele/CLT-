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
  inCharlotteMecklenburg: boolean
  normalizedAddress?: string | null
  latitude?: number | null
  longitude?: number | null
  warning?: string | null
}

export type ResidentAccount = {
  email: string
  displayName?: string | null
  homeAddress?: string | null
  homeLatitude?: number | null
  homeLongitude?: number | null
}

export type AuthProvider = { id: 'Google'|'Microsoft'|'Apple'|'Facebook'; enabled: boolean }
export type CivicPlace = { name: string; address: string; phone?: string|null; distanceMiles?: number|null; website?: string|null }
export type RepresentativeInfo = { chamber: string; district?: number|null; name: string; website: string; phone?: string|null }
export type CivicProfile = {
  address: string
  councilDistrict?: number|null
  councilMember?: string|null
  councilEmail?: string|null
  policeDivision?: string|null
  policeDivisionOffice?: CivicPlace|null
  fireStation?: CivicPlace|null
  dmvOffice?: CivicPlace|null
  nearbySchools: CivicPlace[]
  schoolAssignmentUrl: string
  congressionalDistrict?: number|null
  stateHouseDistrict?: number|null
  stateSenateDistrict?: number|null
  representatives: RepresentativeInfo[]
  postOffice?: CivicPlace|null
  hospitals: CivicPlace[]
  emergencyRoom?: CivicPlace|null
}
