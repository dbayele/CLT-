import type { AddressValidation, AuthProvider, CivicProfile, Draft, ResidentAccount, ResidentVehicle, ResidentVehicleInput, Service, SubmissionResult } from './types'

export const API_BASE = import.meta.env.VITE_API_BASE_URL ?? '/api'
const jsonHeaders = { 'Content-Type': 'application/json' }
const credentials: RequestCredentials = 'include'

async function body<T>(response: Response): Promise<T> {
  const data = await response.json().catch(()=>({}))
  if (!response.ok) throw new Error(data.error ?? data.errors?.join?.(' ') ?? 'Request failed')
  return data
}

export async function getServices(): Promise<Service[]> { return body(await fetch(`${API_BASE}/services`, { credentials })) }
export async function validateAddress(address: string): Promise<AddressValidation> { return body(await fetch(`${API_BASE}/address/validate?address=${encodeURIComponent(address)}`, { credentials })) }
export async function submitRequest(service: Service, draft: Draft): Promise<SubmissionResult> {
  return body(await fetch(`${API_BASE}/requests`, { method:'POST', headers:jsonHeaders, credentials, body:JSON.stringify({serviceId:service.id,location:draft.location,details:draft.details,contact:draft.contact}) }))
}
export async function submitSupplement(trackingNumber: string, payload: { narrative: string; people?: string; property?: string; vehicles?: string; evidence?: string }) {
  return body(await fetch(`${API_BASE}/requests/${encodeURIComponent(trackingNumber)}/supplements`, { method:'POST', headers:jsonHeaders, credentials, body:JSON.stringify(payload) }))
}
export async function trackRequest(trackingNumber: string) { return body<any>(await fetch(`${API_BASE}/requests/${encodeURIComponent(trackingNumber)}`, { credentials })) }

export async function registerResident(email:string,password:string,displayName?:string):Promise<ResidentAccount> {
  return body(await fetch(`${API_BASE}/account/register`, {method:'POST',headers:jsonHeaders,credentials,body:JSON.stringify({email,password,displayName})}))
}
<<<<<<< HEAD
export async function loginResident(email:string,password:string):Promise<ResidentAccount> {
  return body(await fetch(`${API_BASE}/account/login`, {method:'POST',headers:jsonHeaders,credentials,body:JSON.stringify({email,password})}))
}
export async function logoutResident(){ return body(await fetch(`${API_BASE}/account/logout`, {method:'POST',credentials})) }
export async function getResident():Promise<ResidentAccount|null> {
  const r=await fetch(`${API_BASE}/account/me`,{credentials}); if(r.status===401)return null; return body(r)
}
export async function getAuthProviders():Promise<AuthProvider[]> { return body(await fetch(`${API_BASE}/account/providers`,{credentials})) }
export function externalLoginUrl(provider:string){ return `${API_BASE}/account/external/${encodeURIComponent(provider)}` }
export async function saveHomeAddress(address:string):Promise<ResidentAccount> {
  return body(await fetch(`${API_BASE}/account/address`,{method:'PUT',headers:jsonHeaders,credentials,body:JSON.stringify({address})}))
}
export async function getCivicProfile():Promise<CivicProfile> { return body(await fetch(`${API_BASE}/account/civic-profile`,{credentials})) }
export async function getResidentVehicles():Promise<ResidentVehicle[]> { return body(await fetch(`${API_BASE}/account/vehicles`,{credentials})) }
export async function createResidentVehicle(input:ResidentVehicleInput):Promise<ResidentVehicle> { return body(await fetch(`${API_BASE}/account/vehicles`,{method:'POST',headers:jsonHeaders,credentials,body:JSON.stringify(input)})) }
export async function updateResidentVehicle(id:string,input:ResidentVehicleInput):Promise<ResidentVehicle> { return body(await fetch(`${API_BASE}/account/vehicles/${encodeURIComponent(id)}`,{method:'PUT',headers:jsonHeaders,credentials,body:JSON.stringify(input)})) }
export async function deleteResidentVehicle(id:string){ const r=await fetch(`${API_BASE}/account/vehicles/${encodeURIComponent(id)}`,{method:'DELETE',credentials}); if(!r.ok)throw new Error('Unable to delete vehicle') }
=======

export async function createCheckout(trackingNumber: string): Promise<{ url: string; amount: number; currency: string; status: string }> {
  const response = await fetch(`${API_BASE}/requests/${encodeURIComponent(trackingNumber)}/payments/checkout`, { method: 'POST' })
  const body = await response.json()
  if (!response.ok) throw new Error(body.error ?? body.detail ?? 'Unable to start payment')
  return body
}
>>>>>>> origin/main
