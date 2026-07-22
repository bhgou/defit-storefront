# defit. — full-stack commerce platform

Портфолио-проект интернет-магазина одежды: backend на ASP.NET Core 9 и React-клиент с выразительной brutalist-визуальной системой. Backend — ключевая часть решения: модульный монолит с PostgreSQL, Entity Framework Core, cookie-auth, Telegram-интеграцией, каталогом, корзиной, заказами и административными endpoint'ами.

## О проекте

`defit.` — full-stack e-commerce-приложение. Сервер отвечает за доменную модель магазина, миграции БД, авторизацию, checkout и интеграцию с Telegram, а React-клиент предоставляет адаптивный интерфейс каталога и личного кабинета.

## Возможности

- каталог и отдельные страницы товаров;
- выбор размера и управление корзиной;
- авторизация через Telegram с polling статуса;
- оформление заказа, промокоды и экран оплаты;
- личный кабинет и история заказов;
- отслеживание статуса доставки;
- реферальная программа;
- административная панель для товаров и заказов;
- адаптивная вёрстка и поддержка `prefers-reduced-motion`.

## Backend

- ASP.NET Core 9 Minimal API
- Entity Framework Core 9
- PostgreSQL через Npgsql
- Cookie Authentication и Authorization
- Telegram.Bot для авторизации и фоновых уведомлений
- миграции EF Core и автоматическое применение схемы при запуске

Backend организован как модульный монолит:

```text
backend/WebApplication1/
├── Common/Infrastructure/       # DbContext и общая инфраструктура
├── Modules/Account/              # пользователи и Telegram auth
├── Modules/Customers/            # каталог и товары
├── Modules/Orders/               # корзина, промокоды, заказы
├── Modules/Admin/                # административные endpoint'ы
├── Modules/Notifications/        # Telegram background service
└── Migrations/                   # EF Core migrations
```

## Frontend

- React 19
- TypeScript 6
- Vite 8
- CSS без UI-фреймворков
- ESLint

## Структура репозитория

```text
backend/                    # ASP.NET Core API и доменная логика
frontend/                   # React/Vite-клиент
docs/                       # архитектурные решения и API-документация
```

## Локальный запуск

Требуются .NET SDK 9, Node.js 20+ и PostgreSQL.

```bash
dotnet restore backend/WebApplication1/WebApplication1.csproj
dotnet run --project backend/WebApplication1
```

Backend ожидает connection string `Database` и настройки Telegram в конфигурации.

```bash
git clone <repository-url>
cd frontend
npm install
npm run dev
```

Vite выведет локальный адрес приложения в терминале.

## Проверка

```bash
npm run lint
npm run build
```

## Статус

Портфолио-проект с реализованным ASP.NET Core backend и React-клиентом.
