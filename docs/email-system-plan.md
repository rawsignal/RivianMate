# Email System Plan

## Overview

Config-driven, fire-and-forget email system using Hangfire for background processing.

**Provider options:**
- **Resend** - Recommended for hosted/Pro edition (simple API, no daily limits)
- **SMTP** - For self-hosted users (Gmail, Outlook, or any SMTP server)
- **Disabled** - Self-hosted users who don't need emails

---

## Providers

### Resend (Recommended for Hosted/Pro)

| Feature | Value |
|---------|-------|
| Free tier | 3,000 emails/month |
| Daily limit | None |
| API | REST (simple POST) |
| Cost at scale | $20/mo for 50k |

### SMTP (Self-Hosted)

Universal option for self-hosted users. Works with any SMTP server:

| Provider | Host | Port | Notes |
|----------|------|------|-------|
| Gmail | smtp.gmail.com | 587 | Requires app password |
| Outlook/M365 | smtp.office365.com | 587 | Requires app password |
| Amazon SES | email-smtp.region.amazonaws.com | 587 | Requires IAM credentials |
| Mailgun | smtp.mailgun.org | 587 | Free tier available |
| Custom | (user's server) | 25/587/465 | Self-hosted mail server |

### Disabled (Self-Hosted Option)

For users who don't want/need emails:
- Set `Email.Enabled = false`
- Password reset shows "Contact your administrator" message
- Admin can manually reset passwords via admin panel
- All email triggers silently no-op

---

## Architecture

```
Application Code
    │
    │  EmailTrigger.Fire(Trigger.PasswordReset, context)
    ▼
EmailTriggerService
    │  - Check if trigger enabled in config
    │  - Check user preferences (opt-in)
    │  - Resolve recipient email
    │  - Enqueue Hangfire job
    ▼
Hangfire Queue
    │  - Retry on failure (3 attempts)
    │  - Dead letter after max retries
    ▼
EmailSenderJob
    │  - Load template from disk
    │  - Replace {{tokens}} with values
    │  - Wrap in base layout
    │  - Send via Resend API
    │  - Log result to database
    ▼
Resend API
    POST https://api.resend.com/emails
```

---

## Configuration

### appsettings.json

```json
{
  "RivianMate": {
    "Email": {
      "Enabled": true,
      "Provider": "Resend",  // "Resend" or "SMTP"
      "FromAddress": "noreply@rivianmate.com",
      "FromName": "RivianMate",
      "BaseUrl": "https://rivianmate.com",

      "Resend": {
        "ApiKey": ""
      },

      "SMTP": {
        "Host": "smtp.gmail.com",
        "Port": 587,
        "Username": "",
        "Password": "",
        "UseSsl": true
      },

      "Triggers": {
        "PasswordReset": {
          "Enabled": true,
          "Critical": true,
          "Template": "PasswordReset",
          "Subject": "Reset your password"
        },
        "EmailVerification": {
          "Enabled": true,
          "Critical": true,
          "Template": "EmailVerification",
          "Subject": "Verify your email address"
        },
        "SecurityAlert": {
          "Enabled": true,
          "Critical": true,
          "Template": "SecurityAlert",
          "Subject": "Security alert for your account"
        },
        "ChargingComplete": {
          "Enabled": true,
          "Critical": false,
          "Template": "ChargingComplete",
          "Subject": "{{VehicleName}} finished charging"
        },
        "LowBattery": {
          "Enabled": true,
          "Critical": false,
          "Template": "LowBattery",
          "Subject": "{{VehicleName}} battery is low"
        },
        "ChargingInterrupted": {
          "Enabled": true,
          "Critical": false,
          "Template": "ChargingInterrupted",
          "Subject": "{{VehicleName}} charging interrupted"
        },
        "SoftwareUpdate": {
          "Enabled": false,
          "Critical": false,
          "Template": "SoftwareUpdate",
          "Subject": "Update available for {{VehicleName}}"
        }
      }
    }
  }
}
```

### Environment Variables

```bash
# Common
RIVIANMATE__EMAIL__ENABLED=true
RIVIANMATE__EMAIL__PROVIDER=Resend  # or SMTP
RIVIANMATE__EMAIL__FROMADDRESS=noreply@yourdomain.com
RIVIANMATE__EMAIL__BASEURL=https://your-domain.com

# Resend (if Provider=Resend)
RIVIANMATE__EMAIL__RESEND__APIKEY=re_xxxxxxxxxxxxx

# SMTP (if Provider=SMTP)
RIVIANMATE__EMAIL__SMTP__HOST=smtp.gmail.com
RIVIANMATE__EMAIL__SMTP__PORT=587
RIVIANMATE__EMAIL__SMTP__USERNAME=you@gmail.com
RIVIANMATE__EMAIL__SMTP__PASSWORD=your-app-password
RIVIANMATE__EMAIL__SMTP__USESSL=true
```

---

## Trigger Types

### Critical (Always Sent)

Cannot be disabled by users. Required for account security/functionality.

| Trigger | Description |
|---------|-------------|
| `PasswordReset` | Forgot password flow |
| `EmailVerification` | Confirm email ownership |
| `SecurityAlert` | Login from new device, password changed |

### Non-Critical (Opt-In)

Users must enable these in their preferences. Disabled by default.

| Trigger | Description |
|---------|-------------|
| `ChargingComplete` | Vehicle finished charging |
| `LowBattery` | Battery below threshold |
| `ChargingInterrupted` | Charging stopped unexpectedly |
| `SoftwareUpdate` | OTA update available |
| `GearGuardAlert` | Security camera triggered |

---

## User Preferences

### Database Schema

Add to existing `UserPreferences` entity:

```csharp
// Email preferences
public bool EmailNotificationsEnabled { get; set; } = false;  // Master toggle, opt-in
public bool ChargingCompleteEmail { get; set; } = true;       // If master enabled
public bool LowBatteryEmail { get; set; } = true;
public int LowBatteryThreshold { get; set; } = 20;            // Percent
public bool ChargingInterruptedEmail { get; set; } = true;
public bool SoftwareUpdateEmail { get; set; } = false;
public bool GearGuardAlertEmail { get; set; } = true;
```

### Unsubscribe Flow

1. Email footer contains: `Unsubscribe: {{UnsubscribeUrl}}`
2. URL format: `/email/unsubscribe?token={{UnsubscribeToken}}`
3. Token is signed (HMAC) to prevent tampering: `userId:timestamp:signature`
4. Clicking link sets `EmailNotificationsEnabled = false`
5. Shows confirmation page with option to manage preferences

---

## Fire-and-Forget API

### Interface

```csharp
public interface IEmailTrigger
{
    /// <summary>
    /// Fire an email trigger. Looks up user email from UserId in context.
    /// </summary>
    void Fire(string trigger, object context);

    /// <summary>
    /// Fire an email trigger to a specific email address.
    /// </summary>
    void FireTo(string email, string trigger, object context);
}
```

### Usage Examples

```csharp
// Password reset - fires to specific email
_emailTrigger.FireTo(user.Email, Triggers.PasswordReset, new {
    UserName = user.DisplayName ?? user.Email,
    ResetLink = resetUrl,
    ExpiresIn = "24 hours"
});

// Charging complete - looks up user from vehicle
_emailTrigger.Fire(Triggers.ChargingComplete, new {
    UserId = vehicle.OwnerId,
    VehicleName = vehicle.Name ?? "Your Rivian",
    BatteryLevel = session.EndBatteryLevel,
    EnergyAdded = session.EnergyAddedKwh,
    Duration = FormatDuration(session.StartTime, session.EndTime),
    Location = session.LocationName ?? "Unknown"
});
```

### Static Helper (Optional)

```csharp
public static class EmailTrigger
{
    public static void Fire(string trigger, object context)
        => ServiceLocator.Get<IEmailTrigger>().Fire(trigger, context);
}
```

---

## Template System

### Token Replacement

Simple `{{token}}` replacement. No logic, no loops, no conditionals.

```html
<h1>Hi {{UserName}},</h1>
<p>Your {{VehicleName}} finished charging at {{BatteryLevel}}%.</p>
```

Reserved tokens (auto-injected):
- `{{BaseUrl}}` - Site URL for links
- `{{UnsubscribeUrl}}` - Unsubscribe link
- `{{CurrentYear}}` - For copyright
- `{{PreferencesUrl}}` - Link to email preferences

### Template Structure

```
Email/Templates/
├── _Layout.html           # Base wrapper (header, footer, styles)
├── PasswordReset.html     # Content only, inserted into layout
├── EmailVerification.html
├── SecurityAlert.html
├── ChargingComplete.html
├── LowBattery.html
├── ChargingInterrupted.html
└── SoftwareUpdate.html
```

### Layout Composition

```
┌─────────────────────────────────────┐
│  _Layout.html                       │
│  ┌───────────────────────────────┐  │
│  │  Header (logo, title)         │  │
│  ├───────────────────────────────┤  │
│  │                               │  │
│  │  {{Content}}                  │  │  ← Template inserted here
│  │                               │  │
│  ├───────────────────────────────┤  │
│  │  Footer (unsubscribe, legal)  │  │
│  └───────────────────────────────┘  │
└─────────────────────────────────────┘
```

---

## Design Tokens

Extracted from site CSS for email consistency:

```css
/* Colors */
--bg-body: #0a0a0f;
--bg-card: #12121a;
--bg-card-hover: #1a1a24;
--border: #1e1e2e;
--text: #e4e4e7;
--text-muted: #71717a;
--accent: #3b82f6;
--accent-hover: #2563eb;
--success: #22c55e;
--warning: #f59e0b;
--error: #ef4444;

/* Typography */
--font-family: Inter, -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
--font-size-base: 16px;
--font-size-sm: 14px;
--font-size-lg: 18px;
--font-size-xl: 24px;

/* Spacing */
--space-4: 16px;
--space-6: 24px;
--space-8: 32px;

/* Border radius */
--radius: 8px;
```

---

## File Structure

```
src/RivianMate.Api/
├── Email/
│   ├── Configuration/
│   │   ├── EmailConfiguration.cs      # Bind from appsettings
│   │   └── TriggerConfiguration.cs    # Individual trigger config
│   │
│   ├── Services/
│   │   ├── IEmailTrigger.cs           # Fire-and-forget interface
│   │   ├── EmailTriggerService.cs     # Config lookup, preference check, enqueue
│   │   ├── IEmailSender.cs            # Low-level send interface
│   │   ├── ResendEmailSender.cs       # Resend API implementation
│   │   ├── SmtpEmailSender.cs         # SMTP implementation (self-hosted)
│   │   └── EmailTemplateRenderer.cs   # Load template, replace tokens
│   │
│   ├── Jobs/
│   │   └── SendEmailJob.cs            # Hangfire job
│   │
│   ├── Templates/
│   │   ├── _Layout.html
│   │   ├── PasswordReset.html
│   │   ├── EmailVerification.html
│   │   ├── ChargingComplete.html
│   │   └── ... (other templates)
│   │
│   └── EmailServiceExtensions.cs      # DI registration
│
├── Controllers/
│   └── EmailController.cs             # Unsubscribe endpoint
```

---

## Database

### EmailLog Table

For debugging and auditing:

```csharp
public class EmailLog
{
    public int Id { get; set; }
    public Guid? UserId { get; set; }
    public string ToAddress { get; set; } = "";
    public string Trigger { get; set; } = "";
    public string Subject { get; set; } = "";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResendMessageId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

---

## Implementation Phases

### Phase 1: Core Infrastructure (Current Priority)

- [x] Plan document
- [x] Email mockup templates (BaseTemplate, PasswordReset, etc.)
- [ ] `EmailConfiguration` classes
- [ ] `IEmailSender` interface
- [ ] `ResendEmailSender` (API client for hosted/Pro)
- [ ] `SmtpEmailSender` (SMTP client for self-hosted)
- [ ] `EmailTemplateRenderer` (token replacement)
- [ ] `SendEmailJob` (Hangfire job)
- [ ] `EmailTriggerService` (orchestration)
- [ ] Provider selection based on config
- [ ] `BaseTemplate.html` production template
- [ ] `PasswordReset.html` production template
- [ ] Integrate with ASP.NET Identity password reset
- [ ] Add config to appsettings.json
- [ ] "Contact administrator" fallback when emails disabled

### Phase 2: User Preferences

- [ ] Add email preference fields to `UserPreferences`
- [ ] Migration for new fields
- [ ] Email preferences UI in Settings page
- [ ] Unsubscribe endpoint and page

### Phase 3: Vehicle Alerts

- [ ] `ChargingComplete.html` template
- [ ] `LowBattery.html` template
- [ ] Trigger integration in `ChargingTrackingService`
- [ ] Trigger integration in `VehicleStateProcessor`

### Phase 4: Additional Emails

- [ ] `EmailVerification.html` template
- [ ] `SecurityAlert.html` template
- [ ] `ChargingInterrupted.html` template
- [ ] `SoftwareUpdate.html` template
- [ ] Welcome email on registration

### Phase 5: Admin Broadcast Emails

- [ ] `AdminBroadcast.html` template
- [ ] Admin broadcast email form in Admin panel
- [ ] `BroadcastEmailJob` (Hangfire job for batch sending)
- [ ] Broadcast email history/log view

---

## Admin Broadcast Emails

Admin-only feature to send custom emails to all users on the platform. Used for service announcements, outages, important updates, etc.

### Admin Interface

Located in Admin panel (`/admin/email/broadcast`):

```
┌─────────────────────────────────────────────────────────────┐
│  Send Broadcast Email                                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Subject:                                                   │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ Scheduled Maintenance - January 25th                  │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                             │
│  Message:                                                   │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ RivianMate will be undergoing scheduled maintenance   │  │
│  │ on January 25th from 2:00 AM - 4:00 AM EST.          │  │
│  │                                                       │  │
│  │ During this time, the service will be unavailable.   │  │
│  │ Your vehicles will continue to function normally.    │  │
│  │                                                       │  │
│  │ Thank you for your patience.                         │  │
│  └───────────────────────────────────────────────────────┘  │
│                                                             │
│  Recipients: All users ({{UserCount}} users)                │
│                                                             │
│  [ ] Send test email to myself first                        │
│                                                             │
│  [Preview]  [Send to All Users]                             │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Broadcast Email Flow

```
Admin submits broadcast form
    │
    ▼
Validate inputs (subject, message required)
    │
    ▼
Create BroadcastEmail record in database
    │  - Subject, Message, AdminUserId, CreatedAt
    │  - Status: Pending
    ▼
Enqueue BroadcastEmailJob (Hangfire)
    │
    ▼
BroadcastEmailJob executes:
    │  1. Fetch all active users with verified emails
    │  2. For each user (batched):
    │     - Render AdminBroadcast template with message
    │     - Enqueue individual SendEmailJob
    │  3. Update BroadcastEmail status: Completed
    │  4. Record: TotalSent, FailedCount
    ▼
Individual emails sent via normal pipeline
```

### Database Schema

```csharp
public class BroadcastEmail
{
    public int Id { get; set; }
    public Guid AdminUserId { get; set; }
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public BroadcastStatus Status { get; set; }
    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum BroadcastStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}
```

### Template: AdminBroadcast.html

Simple template that displays the admin's custom message:

```html
<!-- Icon -->
<div style="text-align: center; margin-bottom: 24px;">
  <!-- Megaphone/announcement icon -->
</div>

<!-- Title -->
<h1 style="...">{{Subject}}</h1>

<!-- Message (preserves line breaks) -->
<div style="...">
  {{Message}}
</div>

<!-- Footer note -->
<p style="color: #8FA696; font-size: 13px; text-align: center;">
  This is an important announcement from RivianMate.
</p>
```

### Safety Features

1. **Confirmation required**: "Send to All Users" requires typing "SEND" to confirm
2. **Test email option**: Send to admin's own email first to preview
3. **Rate limiting**: Only one broadcast can be in progress at a time
4. **Audit log**: All broadcasts logged with admin user, timestamp, content
5. **Cannot be unsubscribed**: Broadcast emails are critical/operational

### API Endpoint

```csharp
[Authorize(Roles = "Admin")]
[HttpPost("/api/admin/email/broadcast")]
public async Task<IActionResult> SendBroadcast(BroadcastEmailRequest request)
{
    // Validate
    // Create BroadcastEmail record
    // Enqueue BroadcastEmailJob
    // Return broadcast ID for status tracking
}

[Authorize(Roles = "Admin")]
[HttpGet("/api/admin/email/broadcast/{id}/status")]
public async Task<IActionResult> GetBroadcastStatus(int id)
{
    // Return current status, sent count, failed count
}
```

---

## Security Considerations

1. **API Key Storage**: Resend API key in environment variable, not appsettings
2. **Unsubscribe Tokens**: HMAC-signed to prevent unauthorized unsubscribe
3. **Rate Limiting**: Future consideration - prevent email spam
4. **PII in Logs**: Don't log email body, only metadata
5. **Template Injection**: Sanitize user-provided values in templates

---

## Testing

1. **Local Development**: Set `Email.Enabled = false` to skip sending
2. **Resend Test Mode**: Use test API key for development
3. **Template Preview**: Add `/email/preview/{template}` endpoint (dev only)

---

## Rollout Plan

1. Deploy with `Email.Enabled = false`
2. Configure Resend API key
3. Test password reset flow manually
4. Enable in production
5. Monitor Hangfire dashboard for failures
6. Add vehicle alerts in future release
