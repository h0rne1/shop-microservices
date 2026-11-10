#!/usr/bin/env bash
# Минимальный сценарий интернет-магазина через curl.
# Запуск: bash docs/requests.sh   (стек должен быть запущен: docker compose up -d)

CATALOG=http://localhost:5001
ORDERS=http://localhost:5002
PAYMENTS=http://localhost:5003

step() { echo; echo "=== $1"; echo "\$ $2"; eval "$2"; echo; }

step "1. Health-check всех сервисов" \
  "curl -s $CATALOG/health; echo; curl -s $ORDERS/health; echo; curl -s $PAYMENTS/health"

step "2. Каталог товаров (catalog-service)" \
  "curl -s $CATALOG/api/products"

step "3. Добавить новый товар" \
  "curl -s -X POST $CATALOG/api/products -H 'Content-Type: application/json' -d '{\"name\":\"Клавиатура Type One\",\"price\":4990,\"stock\":15}'"

step "4. Оформить заказ: order-service -> catalog-service (резерв) -> payment-service (оплата)" \
  "curl -s -X POST $ORDERS/api/orders -H 'Content-Type: application/json' -d '{\"customerName\":\"Иван Петров\",\"items\":[{\"productId\":2,\"quantity\":1},{\"productId\":3,\"quantity\":2}]}'"

step "5. Остатки уменьшились (товар зарезервирован)" \
  "curl -s $CATALOG/api/products/2; echo; curl -s $CATALOG/api/products/3"

step "6. Платёж создан в payment-service" \
  "curl -s '$PAYMENTS/api/payments?orderId=1'"

step "7. Заказ дороже лимита 100000 -> оплата отклонена, товар возвращается на склад" \
  "curl -s -X POST $ORDERS/api/orders -H 'Content-Type: application/json' -d '{\"customerName\":\"Анна Смирнова\",\"items\":[{\"productId\":1,\"quantity\":2}]}'"

step "8. Остаток ноутбука не изменился (компенсация сработала)" \
  "curl -s $CATALOG/api/products/1"

step "9. Заказ больше, чем есть на складе -> 422 Rejected" \
  "curl -s -w '\nHTTP %{http_code}' -X POST $ORDERS/api/orders -H 'Content-Type: application/json' -d '{\"customerName\":\"Олег\",\"items\":[{\"productId\":4,\"quantity\":100}]}'"

step "10. Все заказы" \
  "curl -s $ORDERS/api/orders"
