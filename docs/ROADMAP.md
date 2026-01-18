# Roadmap

Future features and enhancements planned for RivianMate.

---

## Cloud Edition - Billing & Subscriptions

**Priority:** High (required before cloud launch)

When hosting the cloud edition, we need subscription management:

### Requirements

- [ ] Subscription status tracking per user
- [ ] Payment processing integration (Stripe recommended)
- [ ] Billing portal for users to manage subscription
- [ ] Grace period handling for failed payments
- [ ] Block access to app when subscription lapses (show billing screen only)
- [ ] Webhook handlers for payment events
- [ ] Subscription tiers (if needed in future)

### User Flow

1. New user signs up → starts free trial or must subscribe
2. Active subscriber → full access to cloud features
3. Subscription lapses → redirect to billing screen, no app access
4. User pays → immediately restore access

### Technical Considerations

- Use Stripe for payment processing (well-documented, good Blazor examples)
- Store subscription status in database (`UserSubscription` table)
- Check subscription status on auth/page load
- Stripe webhooks for real-time status updates
- Consider Stripe Customer Portal for self-service billing management

### Files to Create

- `RivianMate.Core/Entities/UserSubscription.cs`
- `RivianMate.Api/Services/BillingService.cs`
- `RivianMate.Api/Controllers/StripeWebhookController.cs`
- `RivianMate.Api/Components/Pages/Billing.razor`
- `RivianMate.Api/Components/Shared/SubscriptionGate.razor`

---

## Future Features

### Cloud-Only Features (to implement)

- [ ] Battery Care Tips - analysis and recommendations
- [ ] Advanced Analytics - trends, comparisons, insights
- [ ] Drive History - trip logging and statistics
- [ ] Data Export - CSV/JSON export of all data
- [ ] Notifications - alerts for charging, battery health, etc.
- [ ] API Access - REST API for third-party integrations

### General Enhancements

- [ ] Mobile-responsive improvements
- [ ] Dark/light theme toggle
- [ ] Email notifications
- [ ] Push notifications (PWA)
- [ ] Multi-language support
- [ ] Charging cost tracking
- [ ] Efficiency comparisons
- [ ] Maintenance reminders

### Infrastructure

- [ ] Health check endpoints
- [ ] Metrics/monitoring integration
- [ ] Rate limiting for API
- [ ] Backup/restore tooling

---

## Completed

- [x] Core dashboard
- [x] Battery health tracking
- [x] Charging session logging
- [x] Configurable dashboard (show/hide/reorder cards)
- [x] Edition system (Self-hosted vs Cloud)
- [x] Feature gating
- [x] User limits enforcement
- [x] Hangfire background job processing
- [x] Multi-user support (up to 4 for self-hosted)
