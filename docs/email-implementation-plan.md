# Email Implementation Plan for RivianMate

## Current State

- **No email service implemented** - ASP.NET Identity email confirmation is disabled
- **AdminEmails** config exists but only used for Hangfire dashboard access
- **Activity feed** tracks 9 event categories that could trigger email alerts
- **Hangfire** available for background job processing (ideal for email queuing)
- **User preferences** entity exists but has no email notification settings

---

## Email Use Cases

### Priority 1: Account Security (Required)
1. **Password Reset** - Users forgot their password
2. **Email Verification** - Confirm email ownership on registration
3. **Security Alerts** - Login from new device/location (optional)

### Priority 2: Vehicle Alerts (High Value)
1. **Charging Complete** - "Your R1T finished charging at 80%"
2. **Low Battery Warning** - "Battery below 20%, 45 miles remaining"
3. **Charging Interrupted** - "Charging stopped unexpectedly"
4. **Security Events** - "Gear Guard triggered at Home"
5. **Software Update Available** - "OTA update 2026.4.0 available"

### Priority 3: Informational (Nice to Have)
1. **Daily/Weekly Digest** - Summary of drives, charging, efficiency
2. **Monthly Report** - Usage statistics, battery health trends
3. **Welcome Email** - Onboarding with setup tips

---

## Provider Options Analysis

### Option A: Your SMTP Server (Recommended for Self-Hosted)

**Pros:**
- Free (no per-email cost)
- Full control over deliverability
- Works offline/private networks
- No external dependencies
- No vendor lock-in

**Cons:**
- You manage deliverability/spam reputation
- Need SPF/DKIM/DMARC setup for rivianmate.com domain
- Requires SMTP credentials configuration

**To use with rivianmate.com domain:**
- Your SMTP server needs to be authorized to send for rivianmate.com
- Add SPF record: `v=spf1 include:your-smtp-server.com ~all`
- Configure DKIM signing if supported
- Add DMARC policy

**Questions to answer:**
1. What SMTP server do you have? (Postfix, Exchange, hosted service?)
2. Can you add DNS records for rivianmate.com?
3. What's the current SPF/DKIM setup for the domain?

---

### Option B: Free Tier Email Services

| Provider | Free Tier | Notes |
|----------|-----------|-------|
| **Resend** | 3,000/month | Modern API, great DX |
| **Brevo** | 300/day (~9,000/month) | Generous, good deliverability |
| **SendGrid** | 100/day (~3,000/month) | Industry standard, Azure integration |
| **Mailgun** | 5,000 first 3 months | Then pay-as-you-go |
| **Postmark** | 100/month (dev) | Best deliverability, limited free |
| **Amazon SES** | $0.10/1,000 emails | Cheapest at scale |

**Recommendation:** Resend or Brevo for simplicity, SES for Pro edition at scale.

---

### Option C: Hybrid Approach (Recommended)

**Self-Hosted Edition:**
- Default: Configurable SMTP (use your server or any SMTP)
- Fallback: Direct SMTP to user's email provider

**Pro Edition (Azure):**
- Primary: Azure Communication Services or SendGrid
- Managed deliverability, no infrastructure to maintain

---

## Proposed Architecture

### Configuration Structure

```json
{
  "RivianMate": {
    "Email": {
      "Enabled": true,
      "Provider": "Smtp",
      "FromAddress": "noreply@rivianmate.com",
      "FromName": "RivianMate",

      "Smtp": {
        "Host": "smtp.your-server.com",
        "Port": 587,
        "UseSsl": true,
        "Username": "",
        "Password": ""
      },

      "Resend": {
        "ApiKey": ""
      },

      "SendGrid": {
        "ApiKey": ""
      },

      "Templates": {
        "UseHtml": true,
        "IncludeLogo": true
      },

      "RateLimits": {
        "MaxPerUserPerHour": 10,
        "MaxPerUserPerDay": 50
      }
    }
  }
}
```

### Environment Variable Support

```bash
# Self-hosted (Docker/Unraid)
RIVIANMATE__EMAIL__ENABLED=true
RIVIANMATE__EMAIL__PROVIDER=Smtp
RIVIANMATE__EMAIL__SMTP__HOST=smtp.your-server.com
RIVIANMATE__EMAIL__SMTP__PORT=587
RIVIANMATE__EMAIL__SMTP__USERNAME=user
RIVIANMATE__EMAIL__SMTP__PASSWORD=secret

# Pro edition
RIVIANMATE__EMAIL__PROVIDER=SendGrid
RIVIANMATE__EMAIL__SENDGRID__APIKEY=SG.xxx
```

---

## Database Schema Changes

### New: EmailNotificationSettings (per user)

```csharp
public class EmailNotificationSettings
{
    public int Id { get; set; }
    public Guid UserId { get; set; }

    // Master toggle
    public bool EmailsEnabled { get; set; } = true;

    // Account emails (always on when emails enabled)
    public bool PasswordResetEnabled { get; set; } = true;
    public bool SecurityAlertsEnabled { get; set; } = true;

    // Vehicle alerts
    public bool ChargingCompleteEnabled { get; set; } = true;
    public bool LowBatteryEnabled { get; set; } = true;
    public int LowBatteryThreshold { get; set; } = 20; // percent
    public bool ChargingInterruptedEnabled { get; set; } = true;
    public bool SecurityEventsEnabled { get; set; } = true;
    public bool SoftwareUpdateEnabled { get; set; } = true;

    // Digest preferences
    public bool DailyDigestEnabled { get; set; } = false;
    public bool WeeklyDigestEnabled { get; set; } = false;
    public int DigestHourUtc { get; set; } = 8; // 8 AM UTC

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### New: EmailLog (for debugging/rate limiting)

```csharp
public class EmailLog
{
    public int Id { get; set; }
    public Guid? UserId { get; set; }
    public string ToAddress { get; set; }
    public string Subject { get; set; }
    public string TemplateId { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; }
}
```

---

## Service Layer Design

### Interfaces

```csharp
// Core interface
public interface IEmailService
{
    Task SendAsync(string to, string subject, string htmlBody, string? textBody = null);
    Task SendTemplateAsync(string to, string templateId, object model);
    bool IsConfigured { get; }
}

// Provider implementations
public class SmtpEmailService : IEmailService { }
public class ResendEmailService : IEmailService { }
public class SendGridEmailService : IEmailService { }

// High-level service for application use
public interface INotificationService
{
    Task SendPasswordResetAsync(ApplicationUser user, string resetLink);
    Task SendEmailVerificationAsync(ApplicationUser user, string verifyLink);
    Task SendChargingCompleteAsync(Vehicle vehicle, ChargingSession session);
    Task SendLowBatteryWarningAsync(Vehicle vehicle, double batteryPercent, double rangeMiles);
    Task SendSecurityAlertAsync(Vehicle vehicle, string alertType, string details);
    Task SendDailyDigestAsync(ApplicationUser user, List<Vehicle> vehicles);
}
```

### Background Jobs (Hangfire)

```csharp
// Immediate alerts via queue
BackgroundJob.Enqueue<INotificationService>(
    s => s.SendChargingCompleteAsync(vehicleId, sessionId));

// Scheduled digests
RecurringJob.AddOrUpdate<DigestEmailJob>(
    "daily-digest",
    job => job.SendDailyDigestsAsync(),
    "0 8 * * *"); // 8 AM UTC daily

RecurringJob.AddOrUpdate<DigestEmailJob>(
    "weekly-digest",
    job => job.SendWeeklyDigestsAsync(),
    "0 8 * * 1"); // 8 AM UTC Mondays
```

---

## Implementation Phases

### Phase 1: Core Infrastructure (Foundation)
- [ ] Create `EmailConfiguration` class
- [ ] Implement `IEmailService` interface
- [ ] Create `SmtpEmailService` (works with any SMTP)
- [ ] Add email settings to `appsettings.json`
- [ ] Create basic email templates (password reset, verification)
- [ ] Integrate with ASP.NET Identity for password reset
- [ ] Enable `RequireConfirmedAccount` (optional, user choice)

### Phase 2: Vehicle Alerts
- [ ] Create `EmailNotificationSettings` entity
- [ ] Add notification preferences UI to user settings
- [ ] Implement charging complete notifications
- [ ] Implement low battery warnings
- [ ] Implement security event alerts
- [ ] Add alert triggers to `WebSocketSubscriptionService`

### Phase 3: Digest Emails
- [ ] Create digest email templates
- [ ] Implement `DigestEmailJob` for Hangfire
- [ ] Build digest data aggregation (drives, charges, efficiency)
- [ ] Schedule recurring jobs

### Phase 4: Pro Edition Enhancements
- [ ] Add SendGrid/Resend implementations
- [ ] Implement email analytics tracking
- [ ] Add click tracking for links (optional)
- [ ] Implement email preferences sync across devices

---

## Template Strategy

### Recommended: Razor Templates

Store in `Templates/Email/`:
- `_Layout.cshtml` - Common header/footer/styling
- `PasswordReset.cshtml`
- `EmailVerification.cshtml`
- `ChargingComplete.cshtml`
- `LowBattery.cshtml`
- `SecurityAlert.cshtml`
- `DailyDigest.cshtml`
- `WeeklyDigest.cshtml`

**Benefits:**
- Razor syntax familiar to ASP.NET developers
- Strong typing with view models
- Preview in browser during development
- Reusable layouts and partials

### Alternative: MJML or Markdown
- MJML: Responsive email framework, compile to HTML
- Markdown: Simple, convert to HTML at send time

---

## Cost Estimates

### Self-Hosted (Your SMTP)
| Item | Cost |
|------|------|
| SMTP Server | $0 (existing) |
| DNS Records | $0 |
| Development | Time only |
| **Total** | **$0/month** |

### Free Tier Services
| Users | Emails/Month | Provider | Cost |
|-------|--------------|----------|------|
| 100 | ~2,000 | Resend | $0 |
| 500 | ~10,000 | Brevo | $0 |
| 1000+ | ~30,000 | SES | ~$3 |

### Pro Edition at Scale
| Users | Emails/Month | Provider | Cost |
|-------|--------------|----------|------|
| 1,000 | 50,000 | SendGrid Pro | ~$20 |
| 5,000 | 250,000 | SES | ~$25 |
| 10,000 | 500,000 | SES | ~$50 |

---

## Questions Before Implementation

1. **Your SMTP Server:**
   - What server/service is it? (Postfix, Exchange, hosted?)
   - Can you configure it to send as `noreply@rivianmate.com`?
   - Do you have access to add SPF/DKIM records?

2. **Immediate Needs:**
   - Do you need password reset functionality now?
   - Should email verification be required for new accounts?

3. **Alert Priorities:**
   - Which vehicle alerts are most important to users?
   - Should alerts be real-time or batched?

4. **Edition Strategy:**
   - Same email provider for self-hosted and Pro?
   - Or SMTP for self-hosted, managed service for Pro?

5. **Template Design:**
   - Brand guidelines for email styling?
   - Include vehicle images in emails?

---

## Recommendation

**For your situation (cheap/free with existing SMTP):**

1. **Start with SMTP provider** - Use your existing server
2. **Implement Phase 1 first** - Password reset is critical for any app
3. **Add vehicle alerts in Phase 2** - High user value, differentiator
4. **Keep SendGrid/Resend as Pro option** - Better deliverability, managed

**Estimated Development Time:**
- Phase 1: 2-3 days
- Phase 2: 3-4 days
- Phase 3: 2-3 days
- Phase 4: 2-3 days

**Total: ~10-13 days for full implementation**

---

## Next Steps

1. Answer the questions above
2. Verify SMTP server can send for rivianmate.com
3. Approve this plan (or request changes)
4. Begin Phase 1 implementation
