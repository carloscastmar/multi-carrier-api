# 0001. Carrier account credentials: store connected accounts, don't take credentials on every call

- **Status:** Accepted
- **Date:** 2026-11-03

## Context

The API creates shipments for its clients through UPS and FedEx. Each shipment is billed to a carrier account, and each call to a carrier needs an OAuth access token. We have to decide:

1. **Whose carrier account pays for a shipment.**
2. **How the API gets the credentials** it needs to call the carrier for that account.

Today `CarrierOptions` (`src/Infrastructure/Carriers/CarriersOptions.cs`) holds one global `ClientId`/`ClientSecret` per carrier. The real adapters are not implemented yet. A client is a single user, and shipments are isolated by `CreatedBy`.

We considered these options:

- **A. Per-call credentials.** The client sends its carrier credentials (`client_id`/`client_secret`, account number) with every request. The API gets a token, uses it and forgets it. We store nothing.
- **B. Client-supplied token.** The client gets the carrier token itself and passes it to us.
- **C. Platform account (reseller).** One master account per carrier ships for everyone. We bill clients for the rate plus a markup.
- **D. Bring your own account (BYOA), connected once.** The API is registered once with each carrier as an integrator/developer app. Each user connects their own carrier account once, and we store the result encrypted. The API gets, caches and refreshes tokens itself.

## Decision

We use **D. Bring your own account, connected once.**

- The platform's developer-app credentials stay in configuration (Key Vault or user secrets), as they do today.
- Each user connects a `CarrierAccount` once. It holds:
  - the carrier and account number;
  - a reference to the encrypted delegated credentials: for UPS, the refresh token from the OAuth authorization-code flow; for FedEx, the child key/secret from integrator registration.
- Shipment requests refer to a connected account by id, or fall back to the user's default account for that carrier. They never carry secrets.
- Adapters get credentials through an `ICarrierCredentialProvider` and cache access tokens until they expire.
- Credentials are encrypted at rest and never returned by the API. Users can disconnect an account, and the carrier lets them revoke access from the carrier's side as well.
- Carrier accounts follow the same isolation rule as shipments: only the creating user (`CreatedBy`) or an `Administrator` can see them.

## Why not per-call credentials (option A)?

Option A is attractive because we would never store a client secret. It is also the simplest thing to build. We rejected it for these reasons.

1. **It doesn't fit how the carriers want third-party platforms to integrate.** UPS and FedEx use OAuth. If a client wants to send us a `client_id`/`client_secret`, the client must first register its *own* developer app with every carrier, and keep it up to date. Sparing clients that work is a main reason this API exists. The carriers' integrator programmes have the platform register once and each customer *authorise* it: UPS through the authorization-code flow, FedEx through integrator/child credentials. Those flows produce a long-lived credential (a refresh token or child key) that the platform must keep somewhere. So under the integration model the carriers expect, "store nothing" is not available.

2. **Secrets would be exposed more often, not less.** A secret sent on every request passes through every hop on every request: TLS termination, proxies, the API host, exception messages, and OpenTelemetry traces and logs (ServiceDefaults turns these on). One accidental logging of a request body or header leaks credentials for every client that made a call in that window. A credential stored once, encrypted and only read inside the adapter, has a much smaller exposure.

3. **Work that happens after the call has no credentials.** Tracking updates, voiding a label, retries after a carrier outage, return labels and webhooks all happen when the client isn't making a call. With per-call credentials, none of these can work unless the client calls again.

4. **Getting a new token on every call costs time and rate limit.** Without stored credentials, either every shipment pays for an extra OAuth round trip, or we cache tokens keyed by the client's secret. The second option means keeping secrets in memory anyway, which is storage in all but name.

5. **It weakens the "one interface" promise.** Clients have to know each carrier's credential format, rotate them and send them. Adding a carrier would mean changing every client integration. That pushes carrier-specific detail outward, which the architecture forbids ("Carrier-specific request/response shapes stay inside the adapter").

6. **It is harder to audit and revoke.** With stored, connected accounts we know which accounts are linked, when they were last used, and we can disconnect them. With per-call credentials, any holder of a valid secret can use our API as a relay, and we can't tell a client's own call from a stolen-credential call.

### What option A would have bought us, and how D handles it

| Concern | Option D answer |
|---|---|
| A database breach leaks carrier secrets | Encrypt at rest with a key held outside the database (ASP.NET Core Data Protection or Key Vault). Store delegated tokens rather than passwords. Each user can revoke access at the carrier. |
| More code to build | One entity, a connect/disconnect use case, a credential provider and a token cache. This is small next to the adapters themselves. |
| Compliance scope | Delegated OAuth credentials are scoped to our app and revocable. That is a narrower scope than holding a client's own developer-app secret. |

### When option A would be acceptable

Option A would be acceptable if this were an internal gateway for a single company that owns both the clients and the carrier accounts, with no background processing. That is not this product.

## Other options rejected

- **B. Client-supplied token:** this has all the problems of A. In addition, every client must implement each carrier's OAuth flow, and nothing can run after the call because the token expires (about one hour).
- **C. Platform account (reseller):**
  - Needs reseller or partner agreements with each carrier.
  - Needs client wallets or credit limits.
  - Makes us liable for the carrier's later billing adjustments, such as dimensional-weight audits and address corrections.
  - We postpone it rather than rule it out: `CarrierAccount` will carry an `IsPlatformAccount` flag so this can be added later without changing the model.

## Consequences

- **New domain entity:** `CarrierAccount`, with a `Create(...)` factory and intent methods. It gets an EF configuration, a migration, and field lengths in `ShipmentFieldLengths` (or a sibling constants class).
- **New use cases:** connect and disconnect a carrier account, plus queries that respect the `CreatedBy`/`Administrator` rule.
- **Shipment creation:** `CreateShipment` gains an optional `CarrierAccountId`.
- **Infrastructure:**
  - credential encryption;
  - `ICarrierCredentialProvider`;
  - a per-account token cache;
  - OAuth callback handling for UPS;
  - the FedEx integrator registration flow.
- **Mock adapters:** they also go through the credential lookup, so functional tests cover it.
- **Billing:** we charge per successfully created label or by subscription. The carrier bills the user directly.
- **Open:** machine-to-machine callers (API keys or OAuth client-credentials) are a separate decision. For now, clients keep authenticating with the existing Identity bearer tokens.
- **Open:** onboarding details depend on our approval in the UPS and FedEx integrator programmes. Until then, a first increment may accept credentials submitted once by the user, stored encrypted, with the OAuth redirect flow added once we are approved.
