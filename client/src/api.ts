import type { Draft, Service, SubmissionResult } from './types'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? '/api'

export async function getServices(): Promise<Service[]> {
  const response = await fetch(`${API_BASE}/services`)
  if (!response.ok) throw new Error('Unable to load services')
  return response.json()
}

export async function submitRequest(service: Service, draft: Draft): Promise<SubmissionResult> {
  const response = await fetch(`${API_BASE}/requests`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      serviceId: service.id,
      location: draft.location,
      details: draft.details,
      contact: draft.contact,
    }),
  })
  const body = await response.json()
  if (!response.ok) throw new Error(body.error ?? 'Unable to submit request')
  return body
}

export async function trackRequest(trackingNumber: string) {
  const response = await fetch(`${API_BASE}/requests/${encodeURIComponent(trackingNumber)}`)
  const body = await response.json()
  if (!response.ok) throw new Error(body.error ?? 'Request not found')
  return body
}
