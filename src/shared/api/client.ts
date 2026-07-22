import type { Product } from '../../entities/product/product'

export const API_URL = 'http://localhost:5201'

export type Profile = {
  id: string
  name: string
  telegramUsername?: string
  photoUrl?: string
  referralCode: string
  createdAt: string
}

export type Account = {
  profile: Profile
  referral: { code: string; link: string; invited: number; reward: number }
}

export type CartItem = Product & { id: string; productId: string; quantity: number; size: string }
export type Cart = { items: CartItem[]; count: number; subtotal: number }

export type Order = {
  id: string
  number: string
  subtotal: number
  discount: number
  total: number
  status: string
  deliveryAddress: string
  promoCode?: string
  trackingNumber: string
  createdAt: string
  estimatedDelivery: string
  items: Array<{ id: string; productName: string; imageUrl: string; unitPrice: number; quantity: number; size: string }>
  timeline: Array<{ key: string; label: string; completed: boolean; active: boolean }>
}

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const isFormData = init?.body instanceof FormData
  const response = await fetch(`${API_URL}${path}`, {
    credentials: 'include',
    ...init,
    headers: isFormData ? { ...init?.headers } : { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const body = await response.json().catch(() => ({}))
    throw new Error(body.error || response.statusText || 'Ошибка запроса')
  }
  if (response.status === 204) return undefined as T
  return response.json()
}
