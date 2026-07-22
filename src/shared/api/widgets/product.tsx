import type { Product } from '../../../entities/product/product'

type ProductCardProps = {
  product: Product
}

const priceFormatter = new Intl.NumberFormat('ru-RU', {
  style: 'currency',
  currency: 'RUB',
  maximumFractionDigits: 0,
})

export function ProductCard({ product }: ProductCardProps) {
  return (
    <a className="product-card" href={`/products/${product.id}`}>
      <div className="product-card__media">
        {product.imageUrl && (
          <img src={product.imageUrl} alt={product.name} />
        )}
      </div>
      <div className="product-card__info">
        <h2>{product.name}</h2>
        <p>{priceFormatter.format(product.price)}</p>
      </div>
    </a>
  )
}

export default ProductCard
