# CLT++ Resident Accounts and My District

This branch adds resident accounts and an address-based civic dashboard to the CLT++ citizen application.

## Account options

Residents can create an account with an email address and password or sign in through a configured external provider:

- Google
- Microsoft account
- Apple
- Facebook

ASP.NET Core Identity stores local accounts and password hashes in SQLite by default. The database location is controlled by `CLTPP_RESIDENT_DB`.

External providers are enabled only when their credentials are present. Configure:

```text
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
MICROSOFT_CLIENT_ID=
MICROSOFT_CLIENT_SECRET=
FACEBOOK_APP_ID=
FACEBOOK_APP_SECRET=
APPLE_CLIENT_ID=
APPLE_CLIENT_SECRET=
CLTPP_PUBLIC_URL=https://your-host.example
```

`APPLE_CLIENT_SECRET` must be a valid Sign in with Apple client-secret JWT generated from the Apple developer key; it is not a normal static password.

Provider callback/redirect URIs must point to the deployed API callback endpoints and must also be registered with the provider.

## My District

A resident can save a validated home address. CLT++ then builds a civic profile containing:

- Charlotte City Council district and council representative/contact
- U.S. House district and representative
- North Carolina House district and representative
- North Carolina Senate district and senator
- North Carolina's two U.S. senators
- CMPD division and nearest division-office result when available
- nearest Charlotte Fire station result
- nearest NCDMV office from the configured Charlotte-area directory
- nearby public schools plus an authoritative CMS Find My School link
- nearest post-office result
- nearby hospitals
- nearest emergency-room result

District geometry is resolved from public GIS/Census data. Officeholder information is a separate mapping and should be refreshed after elections, appointments, redistricting, or vacancy changes.

Nearby school, hospital, ER, and post-office proximity queries use public OpenStreetMap/Overpass data. These results are useful for discovery but should not be treated as an authoritative emergency-routing, school-assignment, or facility-status system.

## My Vehicles

Residents can store vehicles under their own account with:

- license plate and plate state
- year, make, model
- body/car type
- color
- optional VIN
- optional nickname

All vehicle API reads/writes are scoped to the authenticated resident ID. A unique constraint prevents the same state/plate pair from being added twice to one resident account.

## Production checklist

Before production use:

- replace `EnsureCreated` with versioned EF Core migrations
- require email verification for password accounts
- configure account recovery, MFA/passkeys, lockout and fraud controls
- set secure cookie, proxy/HTTPS and data-protection key persistence settings
- complete provider-specific OAuth security review and callback configuration
- encrypt sensitive resident data at rest and establish retention/deletion rules
- replace any static officeholder directory with an administratively managed or authoritative current-data feed
- monitor external GIS/geocoder/location APIs and implement caching/fallback behavior
- perform accessibility, privacy, threat-model and penetration testing
