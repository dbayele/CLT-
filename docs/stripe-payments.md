# Stripe payments for CLT++

CLT++ uses Stripe-hosted Checkout for optional service-request fees. Card numbers, CVCs, and other sensitive payment credentials never pass through the React app or CLT++ API.

## Configuration

Set these environment variables on the ASP.NET Core API:

```bash
STRIPE_SECRET_KEY=sk_test_...
STRIPE_WEBHOOK_SECRET=whsec_...
CLTPP_PUBLIC_URL=http://localhost:5173
CLTPP_STRIPE_FEES=fire-payment=2500,alarm-permit=3000
```

`CLTPP_STRIPE_FEES` is a comma-separated `serviceId=cents` map. The values above are **example test amounts only** and are not City of Charlotte fee schedules. Production fees must come from the authoritative agency fee configuration.

Optional:

```bash
CLTPP_PAYMENT_DATA_PATH=/data/payments.json
```

## Checkout flow

1. A citizen submits a CLT++ service request.
2. If its service ID is configured in `CLTPP_STRIPE_FEES`, request tracking shows the amount and payment status.
3. `POST /api/requests/{trackingNumber}/payments/checkout` creates a Stripe Checkout Session on the server.
4. The browser redirects to the Stripe-hosted Checkout URL.
5. Stripe sends payment events to `POST /api/payments/stripe/webhook`.
6. CLT++ verifies the `Stripe-Signature` using `STRIPE_WEBHOOK_SECRET` and updates payment status.

The success redirect is informational only. CLT++ treats the verified webhook as the source of truth for payment completion.

## Local webhook testing

With the Stripe CLI authenticated to a test account:

```bash
stripe listen --forward-to localhost:5080/api/payments/stripe/webhook
```

Copy the `whsec_...` secret printed by the CLI into `STRIPE_WEBHOOK_SECRET`.

## Production requirements

Use Stripe live-mode keys only through a secret manager, never commit keys to source control, restrict webhook endpoints to HTTPS, keep request/payment audit history in a transactional database, and reconcile successful payments against the agency's finance system before treating a government fee as settled.
