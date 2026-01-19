# Editions

RivianMate is available in two editions.

## Overview

| Aspect | Self-Hosted | Pro |
|--------|:-----------:|:---:|
| **Hosting** | You host it | We host it |
| **Cost** | Free | Subscription |
| **Max Users** | 4 | Unlimited |
| **Max Vehicles/User** | 10 | Unlimited |
| **Max Rivian Accounts/User** | 2 | Unlimited |

## Features by Edition

| Feature | Self-Hosted | Pro |
|---------|:-----------:|:---:|
| Dashboard | Yes | Yes |
| Battery Health | Yes | Yes |
| Vehicle State | Yes | Yes |
| Charging Sessions | Yes | Yes |
| Custom Dashboard | Yes | Yes |
| Battery Care Tips | Yes | Yes |
| Advanced Analytics | Yes | Yes |
| Drive History | Yes | Yes |
| Data Export | Yes | Yes |
| Notifications | - | Yes |
| API Access | - | Yes |

---

## Self-Hosted Edition

The self-hosted edition is free for personal use. Deploy it on your own server, NAS, or home lab.

**Includes:**
- Core dashboard and vehicle monitoring
- Battery health tracking with care tips
- Charging session history
- Drive history
- Advanced analytics
- Data export (CSV, JSON)
- Customizable dashboard layout
- Up to 4 users (perfect for a household)
- 10 vehicles per user
- 2 Rivian accounts per user

**Get started:** See [SELF_HOSTING.md](SELF_HOSTING.md)

---

## Pro Edition

The Pro edition is our managed cloud service with all features and no limits.

**Includes everything in self-hosted, plus:**
- Push notifications and alerts
- REST API access for integrations
- No user/vehicle limits
- Automatic updates
- Managed hosting
- Support

**Sign up:** Coming soon

---

## Technical Implementation

Editions are determined at **compile time**, not runtime. This means:

- The Pro binary contains all code
- The Self-Hosted binary excludes Pro-only features entirely
- There's no way to "unlock" Pro features in a Self-Hosted build

This is implemented using C# preprocessor directives:

```csharp
#if EDITION_PRO
    // Pro-only code
#endif
```

Build for a specific edition:

```bash
# Self-Hosted (default)
dotnet build

# Pro
dotnet build -p:Edition=Pro
```

See `BuildInfo.DisplayName` to verify which edition is running - the navbar displays "RivianMate Pro" for Pro builds.

---

## FAQ

### Why the limits on self-hosted?

Self-hosted is designed for personal and family use. The limits prevent unauthorized commercial use while covering typical household needs.

### Is the self-hosted edition really free?

Yes. No time limits, no nag screens, no telemetry. Core features work forever.

### What happens if I exceed the user limit?

New user registrations are blocked. Existing users continue to work normally.

### Can I upgrade later?

You can migrate to the Pro edition anytime. Your local database can be backed up and imported.

### Why are some features Pro-only?

Features like notifications and API access require ongoing infrastructure (push servers, API rate limiting, etc.) that only make sense in a managed environment.
