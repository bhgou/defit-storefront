import type { Product } from '../../entities/product/product'

const API_URL = 'http://localhost:5201'

export async function getAllProducts(): Promise<Product[]> {
  const response = await fetch(`${API_URL}/api/products`)

  if (!response.ok) {
    throw new Error(response.statusText)
  }

  return response.json()
}
  