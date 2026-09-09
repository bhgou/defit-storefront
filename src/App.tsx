import { useCallback, useEffect, useMemo, useState } from 'react'
import './App.css'
import type { Product } from './entities/product/product'
import { getAllProducts } from './shared/api/products'
import { api, type Account, type Cart, type Order } from './shared/api/client'

const STORE = 'defit.'
const money = new Intl.NumberFormat('ru-RU', { style: 'currency', currency: 'RUB', maximumFractionDigits: 0 })
const emptyCart: Cart = { items: [], count: 0, subtotal: 0 }

function Test3(){
  
}


function BrandIntro({ hidden = false }: { hidden?: boolean }) {
  const [text, setText] = useState('')
  useEffect(() => {
    const brand = STORE
    let index = 0
    const timer = window.setInterval(() => {
      index += 1
      setText(brand.slice(0, index))
      if (index === brand.length) window.clearInterval(timer)
    }, 140)
    return () => window.clearInterval(timer)
  }, [])
  return <div className={hidden ? 'brand-intro brand-intro--hidden' : 'brand-intro brand-intro--visible'} aria-label={STORE}><span>{text}</span></div>
}

function useStore() {
  const [account, setAccount] = useState<Account | null>(null)
  const [cart, setCart] = useState<Cart>(emptyCart)
  const [authChecked, setAuthChecked] = useState(false)

  const refresh = useCallback(async () => {
    try {
      const me = await api<Account>('/api/account/me')
      setAccount(me)
      setCart(await api<Cart>('/api/cart/'))
    } catch {
      setAccount(null)
      setCart(emptyCart)
    } finally {
      setAuthChecked(true)
    }
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => void refresh(), 0)
    return () => window.clearTimeout(timer)
  }, [refresh])
  return { account, cart, setCart, authChecked, refresh }
}

function Header({ account, count }: { account: Account | null; count: number }) {
  const [open, setOpen] = useState(false)
  return (
    <header className="header">
      <a className="wordmark" href="/">{STORE}</a>
      <button className="menu-toggle" onClick={() => setOpen(!open)} aria-label="Меню">{open ? 'ЗАКРЫТЬ' : 'МЕНЮ'}</button>
      <nav className={open ? 'nav nav--open' : 'nav'}>
        <a href="/orders">МОИ ЗАКАЗЫ</a>
        <a href={account ? '/account' : '/register'}>{account ? account.profile.name : 'ВОЙТИ'}</a>
        <a className="cart-link" href="/cart">КОРЗИНА <span>{String(count).padStart(2, '0')}</span></a>
      </nav>
    </header>
  )
}

function Home({ products }: { products: Product[] }) {
  return (
    <main className="catalog-page">
      <div className="catalog-head"><span>{STORE.toUpperCase()}</span><span>{products.length} ТОВАРОВ</span></div>
      <div className="products">
        {products.map((product, index) => <ProductTile key={product.id} product={product} index={index} />)}
        {!products.length && <div className="empty-state">КАТАЛОГ ПУСТ.</div>}
      </div>
    </main>
  )
}

function ProductTile({ product, index }: { product: Product; index: number }) {
  return (
    <a className="product" href={`/products/${product.id}`}>
      <div className="product__image">
        {product.imageUrl ? <img src={product.imageUrl} alt={product.name} /> : <span>{String(index + 1).padStart(2, '0')}</span>}
        <em>0{index + 1}</em>
      </div>
      <div className="product__meta"><h3>{product.name}</h3><p>{money.format(product.price)}</p></div>
    </a>
  )
}

function Login({ onComplete }: { onComplete: () => Promise<void> }) {
  const [state, setState] = useState<'idle' | 'waiting' | 'error'>('idle')
  const [message, setMessage] = useState('')

  const login = async () => {
    setState('waiting')
    setMessage('ПОДТВЕРДИ ВХОД В TELEGRAM')
    const popup = window.open('', '_blank')
    try {
      const referralCode = new URLSearchParams(location.search).get('ref')
      const result = await api<{ sessionId: string; telegramUrl: string }>('/api/auth/telegram/start', {
        method: 'POST', body: JSON.stringify({ referralCode }),
      })
      if (popup) popup.location.href = result.telegramUrl
      const started = Date.now()
      const poll = window.setInterval(async () => {
        if (Date.now() - started > 5 * 60_000) {
          clearInterval(poll); setState('error'); setMessage('ССЫЛКА ИСТЕКЛА — ПОПРОБУЙ ЕЩЁ РАЗ'); return
        }
        try {
          const status = await api<{ status: string }>(`/api/auth/telegram/status/${result.sessionId}`)
          if (status.status === 'completed') {
            clearInterval(poll); await onComplete(); location.href = '/account'
          }
        } catch (error) { console.debug(error) }
      }, 1800)
    } catch (error) {
      popup?.close(); setState('error'); setMessage(error instanceof Error ? error.message : 'ОШИБКА')
    }
  }

  return (
    <main className="login-page">
      <div className="login-page__count">01</div>
      <p className="eyebrow">ЕДИНЫЙ АККАУНТ {STORE.toUpperCase().replace('.', '')}</p>
      <h1>ВОЙТИ<br />ЧЕРЕЗ <u>TELEGRAM</u></h1>
      <p className="login-page__note">БЕЗ ПАРОЛЯ. ОДНО НАЖАТИЕ.<br />ЗАКАЗЫ И ДОСТАВКА — В ОДНОМ МЕСТЕ.</p>
      <button className="primary" onClick={login} disabled={state === 'waiting'}>
        <span>{state === 'waiting' ? 'ОЖИДАЕМ ПОДТВЕРЖДЕНИЕ' : 'ОТКРЫТЬ TELEGRAM'}</span><b>↗</b>
      </button>
      {message && <p className={`login-status login-status--${state}`}>{message}</p>}
      <small>ПРОДОЛЖАЯ, ТЫ СОГЛАШАЕШЬСЯ С ПОЛИТИКОЙ КОНФИДЕНЦИАЛЬНОСТИ.</small>
    </main>
  )
}

function ProductPage({ id, account, onCart }: { id: string; account: Account | null; onCart: (cart: Cart) => void }) {
  const [product, setProduct] = useState<Product | null>(null)
  const [size, setSize] = useState('M')
  const [busy, setBusy] = useState(false)
  useEffect(() => { api<Product>(`/api/products/${id}`).then(setProduct).catch(() => setProduct(null)) }, [id])
  if (!product) return <main className="loading">ЗАГРУЗКА / PRODUCT</main>

  const add = async () => {
    if (!account) { location.href = `/register?next=/products/${id}`; return }
    setBusy(true)
    try {
      onCart(await api<Cart>('/api/cart/items', { method: 'POST', body: JSON.stringify({ productId: id, quantity: 1, size }) }))
      location.href = '/cart'
    } finally { setBusy(false) }
  }

  return (
    <main className="product-page">
      <div className="product-page__media">{product.imageUrl && <img src={product.imageUrl} alt={product.name} />}<span>{STORE.toUpperCase()} / DROP 01</span></div>
      <section className="product-page__details">
        <p className="eyebrow">DROP 01 / LIMITED</p><h1>{product.name}</h1><p className="price">{money.format(product.price)}</p>
        <p className="description">ПЛОТНЫЙ МАТЕРИАЛ. СВОБОДНЫЙ СИЛУЭТ. СОЗДАНО, ЧТОБЫ НОСИТЬ КАЖДЫЙ ДЕНЬ И НЕ БЫТЬ КАК ВСЕ.</p>
        <div className="size-row"><span>РАЗМЕР</span>{['S', 'M', 'L', 'XL'].map(x => <button className={size === x ? 'active' : ''} onClick={() => setSize(x)} key={x}>{x}</button>)}</div>
        <button className="primary primary--dark" onClick={add} disabled={busy}><span>{busy ? 'ДОБАВЛЯЕМ' : 'В КОРЗИНУ'}</span><b>+</b></button>
        <dl><div><dt>ДОСТАВКА</dt><dd>ПО РОССИИ ОТ 2 ДНЕЙ</dd></div><div><dt>ВОЗВРАТ</dt><dd>14 ДНЕЙ</dd></div></dl>
      </section>
    </main>
  )
}

function CartPage({ cart, setCart, account }: { cart: Cart; setCart: (cart: Cart) => void; account: Account | null }) {
  const [promo, setPromo] = useState('')
  const [discount, setDiscount] = useState(0)
  const [customerName, setCustomerName] = useState('')
  const [phone, setPhone] = useState('')
  const [address, setAddress] = useState('')
  const [notice, setNotice] = useState('')
  const update = async (id: string, quantity: number) => setCart(await api<Cart>(`/api/cart/items/${id}`, { method: 'PATCH', body: JSON.stringify({ quantity }) }))
  const applyPromo = async () => {
    try { const r = await api<{ discountPercent: number }>('/api/promocodes/validate', { method: 'POST', body: JSON.stringify({ code: promo }) }); setDiscount(r.discountPercent); setNotice(`СКИДКА ${r.discountPercent}% ПРИМЕНЕНА`) }
    catch (e) { setDiscount(0); setNotice(e instanceof Error ? e.message : 'ПРОМОКОД НЕ ПРИНЯТ') }
  }
  const checkout = async () => {
    try { const order = await api<Order>('/api/orders/', { method: 'POST', body: JSON.stringify({ customerName, phone, deliveryAddress: address, promoCode: promo }) }); location.href = `/payment/${order.id}` }
    catch (e) { setNotice(e instanceof Error ? e.message : 'ОШИБКА') }
  }
  if (!account) return <Gate title="КОРЗИНА ЖДЁТ ТЕБЯ" />
  return (
    <main className="cart-page">
      <div className="page-title"><p>02 / CART</p><h1>КОРЗИНА</h1><span>{String(cart.count).padStart(2, '0')} ITEMS</span></div>
      {!cart.items.length ? <div className="empty-state"><p>ЗДЕСЬ ПОКА ПУСТО.</p><a className="primary" href="/#drop"><span>СМОТРЕТЬ DROP</span><b>→</b></a></div> : <div className="cart-layout">
        <section className="cart-items">{cart.items.map(item => <article className="cart-item" key={item.id}>
          <img src={item.imageUrl} alt="" /><div><h2>{item.name}</h2><p>РАЗМЕР / {item.size}</p><div className="qty"><button onClick={() => update(item.id, item.quantity - 1)}>−</button><span>{item.quantity}</span><button onClick={() => update(item.id, item.quantity + 1)}>+</button></div></div><strong>{money.format(item.price * item.quantity)}</strong>
        </article>)}</section>
        <aside className="summary"><p className="eyebrow">ДАННЫЕ ДОСТАВКИ</p><input className="address" value={customerName} onChange={e => setCustomerName(e.target.value)} placeholder="ФИО ПОЛУЧАТЕЛЯ" /><input className="address" value={phone} onChange={e => setPhone(e.target.value)} placeholder="НОМЕР ТЕЛЕФОНА" type="tel" /><input className="address" value={address} onChange={e => setAddress(e.target.value)} placeholder="ГОРОД, УЛИЦА, ДОМ, КВАРТИРА" /><p className="eyebrow summary-label">ИТОГО</p><div><span>ТОВАРЫ</span><b>{money.format(cart.subtotal)}</b></div><div><span>СКИДКА</span><b>− {money.format(cart.subtotal * discount / 100)}</b></div><div className="promo"><input value={promo} onChange={e => setPromo(e.target.value.toUpperCase())} placeholder="ПРОМОКОД" /><button onClick={applyPromo}>→</button></div><p className="notice">{notice}</p><div className="summary__total"><span>К ОПЛАТЕ</span><b>{money.format(cart.subtotal * (1 - discount / 100))}</b></div><button className="primary" onClick={checkout}><span>ОФОРМИТЬ ЗАКАЗ</span><b>→</b></button>
        </aside>
      </div>}
    </main>
  )
}

function AccountPage({ account, refresh }: { account: Account | null; refresh: () => Promise<void> }) {
  const [orders, setOrders] = useState<Order[]>([])
  const [copied, setCopied] = useState(false)
  useEffect(() => { if (account) api<Order[]>('/api/orders/').then(setOrders) }, [account])
  if (!account) return <Gate title="ЛИЧНЫЙ КАБИНЕТ" />
  const logout = async () => { await api('/api/auth/logout', { method: 'POST' }); await refresh(); location.href = '/' }
  const copy = async () => { await navigator.clipboard.writeText(account.referral.link); setCopied(true) }
  return (
    <main className="account-page">
      <div className="account-hero"><p>03 / ACCOUNT</p><h1>ПРИВЕТ,<br />{account.profile.name.split(' ')[0]}.</h1><div className="account-id">ID / {account.profile.id.slice(0, 8).toUpperCase()}<br />@{account.profile.telegramUsername || 'TELEGRAM'}</div></div>
      <div className="account-grid">
        <section className="account-panel orders-panel"><div className="panel-head"><h2>МОИ ЗАКАЗЫ</h2><span>{String(orders.length).padStart(2, '0')}</span></div>{orders.length ? orders.map(o => <a href={`/orders/${o.id}`} className="order-row" key={o.id}><b>{o.number}</b><span>{new Date(o.createdAt).toLocaleDateString('ru-RU')}</span><span>{o.status.toUpperCase()}</span><strong>{money.format(o.total)}</strong><i>→</i></a>) : <div className="panel-empty">ТЫ ЕЩЁ НИЧЕГО НЕ ЗАКАЗЫВАЛ.<a href="/#drop">СМОТРЕТЬ DROP →</a></div>}</section>
        <section className="account-panel referral"><div className="panel-head"><h2>РЕФЕРАЛЬНАЯ<br />ПРОГРАММА</h2><span>04</span></div><p>ПРИГЛАСИ ДРУГА — ПОЛУЧИТЕ ПО 500 ₽ ПОСЛЕ ЕГО ПЕРВОГО ЗАКАЗА.</p><div className="ref-code">{account.referral.code}</div><button onClick={copy}>{copied ? 'СКОПИРОВАНО' : 'СКОПИРОВАТЬ ССЫЛКУ'} ↗</button><dl><div><dt>ПРИГЛАШЕНО</dt><dd>{account.referral.invited}</dd></div><div><dt>НА БАЛАНСЕ</dt><dd>{money.format(account.referral.reward)}</dd></div></dl></section>
        <section className="account-panel promo-panel"><div className="panel-head"><h2>ТВОИ ПРОМОКОДЫ</h2><span>02</span></div><div className="coupon"><b>FIRST10</b><span>−10%</span><small>НА ПЕРВЫЙ ЗАКАЗ</small></div><div className="coupon"><b>DEFIT15</b><span>−15%</span><small>ДЛЯ СВОИХ / LIMITED</small></div></section>
        <section className="account-panel settings"><div className="panel-head"><h2>АККАУНТ</h2><span>05</span></div><p>{account.profile.name}</p><p>@{account.profile.telegramUsername || 'TELEGRAM'}</p><a className="admin-link" href="/admin">ОТКРЫТЬ АДМИНКУ →</a><button onClick={logout}>ВЫЙТИ ИЗ АККАУНТА →</button></section>
      </div>
    </main>
  )
}

function OrderPage({ id, account }: { id: string; account: Account | null }) {
  const [order, setOrder] = useState<Order | null>(null)
  useEffect(() => { if (account) api<Order>(`/api/orders/${id}`).then(setOrder) }, [id, account])
  if (!account) return <Gate title="ОТСЛЕЖИВАНИЕ" />
  if (!order) return <main className="loading">ЗАГРУЗКА / TRACKING</main>
  return <main className="tracking"><p className="eyebrow">ЗАКАЗ {order.number}</p><h1>ТВОЙ ЗАКАЗ<br />В ДВИЖЕНИИ.</h1><div className="tracking-grid"><section><div className="track-number"><span>ТРЕК-НОМЕР</span><b>{order.trackingNumber}</b></div><div className="timeline">{order.timeline.map((step, i) => <div className={`${step.completed ? 'done' : ''} ${step.active ? 'active' : ''}`} key={step.key}><i>{String(i + 1).padStart(2, '0')}</i><span>{step.label}</span><b>{step.active ? 'СЕЙЧАС' : step.completed ? 'ГОТОВО' : 'СКОРО'}</b></div>)}</div></section><aside><p>АДРЕС</p><b>{order.deliveryAddress}</b><p>ОЖИДАЕМАЯ ДОСТАВКА</p><b>{new Date(order.estimatedDelivery).toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' }).toUpperCase()}</b><p>СУММА</p><b>{money.format(order.total)}</b></aside></div></main>
}

function PaymentPage({ id, account }: { id: string; account: Account | null }) {
  const [order, setOrder] = useState<Order | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  useEffect(() => { if (account) api<Order>(`/api/orders/${id}`).then(setOrder).catch(() => setError('ЗАКАЗ НЕ НАЙДЕН')) }, [id, account])
  if (!account) return <Gate title="ОПЛАТА" />
  if (!order) return <main className="loading">{error || 'ЗАГРУЗКА / PAYMENT'}</main>
  const pay = async () => {
    setBusy(true); setError('')
    try { await api<Order>(`/api/orders/${id}/pay`, { method: 'POST' }); location.href = `/orders/${id}` }
    catch (reason) { setError(reason instanceof Error ? reason.message : 'НЕ УДАЛОСЬ ОПЛАТИТЬ') }
    finally { setBusy(false) }
  }
  return <main className="payment-page"><section className="payment-card"><div className="payment-card__head"><span>{STORE.toUpperCase()}</span><span>ОПЛАТА ЗАКАЗА</span></div><p>ЗАКАЗ {order.number}</p><h1>{money.format(order.total)}</h1><div className="payment-details"><span>ТОВАРОВ</span><b>{order.items.reduce((sum, item) => sum + item.quantity, 0)}</b><span>ДОСТАВКА</span><b>РАССЧИТАНА</b></div><button className="primary primary--dark" onClick={pay} disabled={busy || order.status !== 'created'}><span>{order.status === 'paid' ? 'УЖЕ ОПЛАЧЕНО' : busy ? 'ОБРАБАТЫВАЕМ' : 'ОПЛАТИТЬ'}</span><b>→</b></button>{order.status === 'paid' && <a className="payment-link" href={`/orders/${id}`}>ПЕРЕЙТИ К ЗАКАЗУ →</a>}{error && <p className="notice">{error}</p>}<small>ТЕСТОВЫЙ РЕЖИМ. СПИСАНИЕ С КАРТЫ НЕ ПРОИЗВОДИТСЯ.</small></section></main>
}

function Gate({ title }: { title: string }) { return <main className="gate"><p className="eyebrow">ДОСТУП ТОЛЬКО ДЛЯ СВОИХ</p><h1>{title}</h1><a className="primary" href="/register"><span>ВОЙТИ ЧЕРЕЗ TELEGRAM</span><b>↗</b></a></main> }
function Info({ type }: { type: string }) { const about = type === 'about'; return <main className="info-page"><p className="eyebrow">{about ? '00 / МАНИФЕСТ' : `INFO / ${STORE.toUpperCase()}`}</p><h1>{about ? 'МЫ НЕ ДЕЛАЕМ\nОДЕЖДУ ДЛЯ ВСЕХ.' : type.toUpperCase()}</h1><p>{about ? `${STORE.toUpperCase()} — НЕЗАВИСИМЫЙ БРЕНД ИЗ САМАРЫ. НАС ИНТЕРЕСУЕТ НЕ МОДА, А ХАРАКТЕР. МЫ ДЕЛАЕМ МАЛЫЕ ТИРАЖИ, ЧЕСТНЫЕ ВЕЩИ И НЕ ПОВТОРЯЕМ DROP.` : 'ПОДРОБНАЯ ИНФОРМАЦИЯ СКОРО ПОЯВИТСЯ ЗДЕСЬ. ПО ВСЕМ ВОПРОСАМ НАПИШИ НАМ В TELEGRAM.'}</p></main> }

type AdminProduct = Product
type AdminOrder = { id: string; number: string; status: string; total: number; customerName: string; phone: string; deliveryAddress: string; trackingNumber: string; createdAt: string; items: number }

function AdminPage({ account }: { account: Account | null }) {
  const [products, setProducts] = useState<AdminProduct[]>([])
  const [orders, setOrders] = useState<AdminOrder[]>([])
  const [name, setName] = useState('')
  const [price, setPrice] = useState('')
  const [imageUrl, setImageUrl] = useState('')
  const [uploading, setUploading] = useState(false)
  const [message, setMessage] = useState('')
  const load = useCallback(async () => {
    try {
      const [nextProducts, nextOrders] = await Promise.all([api<AdminProduct[]>('/api/admin/products'), api<AdminOrder[]>('/api/admin/orders')])
      setProducts(nextProducts); setOrders(nextOrders)
    } catch (error) { setMessage(error instanceof Error ? error.message : 'НЕТ ДОСТУПА') }
  }, [])
  useEffect(() => {
    if (!account) return
    const timer = window.setTimeout(() => void load(), 0)
    return () => window.clearTimeout(timer)
  }, [account, load])
  if (!account) return <Gate title="АДМИНКА" />
  const addProduct = async () => {
    try { await api('/api/admin/products', { method: 'POST', body: JSON.stringify({ name, price: Number(price), imageUrl }) }); setName(''); setPrice(''); setImageUrl(''); setMessage('ТОВАР ДОБАВЛЕН'); await load() }
    catch (error) { setMessage(error instanceof Error ? error.message : 'ОШИБКА') }
  }
  const uploadImage = async (file?: File) => {
    if (!file) return
    setUploading(true)
    try {
      const form = new FormData()
      form.append('file', file)
      const result = await api<{ url: string }>('/api/admin/uploads', { method: 'POST', body: form })
      setImageUrl(result.url); setMessage('ИЗОБРАЖЕНИЕ ЗАГРУЖЕНО')
    } catch (error) { setMessage(error instanceof Error ? error.message : 'ОШИБКА ЗАГРУЗКИ') }
    finally { setUploading(false) }
  }
  const updateStatus = async (id: string, status: string) => { await api(`/api/admin/orders/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }); await load() }
  return <main className="admin-page">
    <div className="page-title"><p>ADMIN / {STORE.toUpperCase()}</p><h1>УПРАВЛЕНИЕ</h1><span>{message}</span></div>
    <div className="admin-grid">
      <section className="admin-panel"><div className="panel-head"><h2>НОВЫЙ ТОВАР</h2><span>01</span></div><input value={name} onChange={event => setName(event.target.value)} placeholder="НАЗВАНИЕ" /><input value={price} onChange={event => setPrice(event.target.value)} placeholder="ЦЕНА" inputMode="decimal" /><label className="file-upload">{uploading ? 'ЗАГРУЗКА...' : 'ЗАГРУЗИТЬ С КОМПЬЮТЕРА'}<input type="file" accept="image/png,image/jpeg,image/webp,image/gif" onChange={event => void uploadImage(event.target.files?.[0])} /></label><input value={imageUrl} onChange={event => setImageUrl(event.target.value)} placeholder="ИЛИ URL ИЗОБРАЖЕНИЯ" /><button className="primary primary--dark" onClick={addProduct}><span>ДОБАВИТЬ ТОВАР</span><b>+</b></button></section>
      <section className="admin-panel"><div className="panel-head"><h2>ТОВАРЫ</h2><span>{products.length}</span></div>{products.map(product => <div className="admin-row" key={product.id}><b>{product.name}</b><span>{money.format(product.price)}</span></div>)}</section>
      <section className="admin-panel admin-orders"><div className="panel-head"><h2>ЗАКАЗЫ</h2><span>{orders.length}</span></div>{orders.map(order => <div className="admin-row admin-order" key={order.id}><div><b>{order.number}</b><small>{order.customerName} · {order.phone}<br />{order.deliveryAddress}</small></div><span>{money.format(order.total)}</span><select value={order.status} onChange={event => void updateStatus(order.id, event.target.value)}><option value="created">СОЗДАН</option><option value="paid">ОПЛАЧЕН</option><option value="packing">СБОРКА</option><option value="shipped">В ДОСТАВКЕ</option><option value="delivered">ДОСТАВЛЕН</option><option value="cancelled">ОТМЕНЁН</option></select></div>)}</section>
    </div>
  </main>
}

function Footer() { return <footer><div className="footer-logo">{STORE}</div><div><p>НАВИГАЦИЯ</p><a href="/">КАТАЛОГ</a><a href="/account">АККАУНТ</a><a href="/cart">КОРЗИНА</a></div><div><p>ИНФОРМАЦИЯ</p><a href="/delivery">ДОСТАВКА</a><a href="/returns">ВОЗВРАТ</a><a href="/privacy">ПРИВАТНОСТЬ</a></div><div><p>СВЯЗЬ</p><a href="https://t.me/testtesttessfsdfsd_bot">TELEGRAM ↗</a><a href="mailto:hello@defit.store">EMAIL ↗</a></div><small>© 2026 / SAMARA / ALL RIGHTS RESERVED</small></footer> }

export default function App() {
  const { account, cart, setCart, authChecked, refresh } = useStore()
  const [products, setProducts] = useState<Product[]>([])
  const [showIntro, setShowIntro] = useState(true)
  useEffect(() => { getAllProducts().then(setProducts).catch(() => setProducts([])) }, [])
  useEffect(() => {
    const timer = window.setTimeout(() => setShowIntro(false), 1500)
    return () => window.clearTimeout(timer)
  }, [])
  const path = location.pathname.replace(/\/$/, '') || '/'
  const content = useMemo(() => {
    if (path === '/') return <Home products={products} />
    if (path === '/register') return <Login onComplete={refresh} />
    if (path === '/cart') return <CartPage cart={cart} setCart={setCart} account={account} />
    if (path === '/account' || path === '/orders') return <AccountPage account={account} refresh={refresh} />
    if (path === '/admin') return <AdminPage account={account} />
    if (path.startsWith('/products/')) return <ProductPage id={path.split('/').pop()!} account={account} onCart={setCart} />
    if (path.startsWith('/orders/')) return <OrderPage id={path.split('/').pop()!} account={account} />
    if (path.startsWith('/payment/')) return <PaymentPage id={path.split('/').pop()!} account={account} />
    return <Info type={path.slice(1) || '404'} />
  }, [path, products, account, cart, setCart, refresh])
  if (!authChecked) return <BrandIntro />
  return <><BrandIntro hidden={!showIntro} /><div className="site"><Header account={account} count={cart.count} />{content}<Footer /></div></>
}
