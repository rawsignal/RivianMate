# Email Verification Implementation Plan

## Overview

Implement email verification that:
- Does NOT block Rivian account linking or app usage initially
- Deactivates accounts after 7 days if email remains unverified
- Shows a dashboard notification (similar to Service Mode) for unverified users
- Allows existing users to verify without re-registering

---

## Components

### 1. Database Changes

ASP.NET Identity already has `EmailConfirmed` on `ApplicationUser`. Add:

```csharp
// ApplicationUser.cs
public DateTime? EmailVerificationSentAt { get; set; }  // For resend cooldown
public DateTime? EmailVerificationDeadline { get; set; } // 7 days from registration
public bool IsDeactivated { get; set; }  // Account deactivation flag
public string? DeactivationReason { get; set; }  // "EmailNotVerified", etc.
```

Migration adds these columns to `AspNetUsers`.

### 2. Email Template

Create `EmailVerification.html`:
- Welcome message
- "Verify your email" button with link
- Mention 7-day deadline
- Security note about not sharing link

### 3. Registration Flow Changes

**Register.razor** updates:
1. After successful registration, set `EmailVerificationDeadline = DateTime.UtcNow.AddDays(7)`
2. Send verification email via `EmailTrigger.FireEmailVerification()`
3. Redirect to dashboard (allow full access)

### 4. Dashboard Notification Component

Create `EmailVerificationBanner.razor`:
- Only shows when `EmailConfirmed == false && !IsDeactivated`
- Yellow/warning style (similar to Service Mode)
- Shows days remaining until deactivation
- "Resend verification email" button with cooldown
- Dismissable per-session (but reappears on next login)

```
┌─────────────────────────────────────────────────────────────────────┐
│ ⚠️  Please verify your email address                                │
│                                                                     │
│ Your account will be deactivated in 5 days if not verified.        │
│                                                                     │
│ [Resend Verification Email]                              [Dismiss] │
└─────────────────────────────────────────────────────────────────────┘
```

### 5. Verification Endpoint

Create `VerifyEmail.razor` page at `/verify-email`:
- Accepts `?userId=xxx&token=xxx` query params
- Validates token using `UserManager.ConfirmEmailAsync()`
- Shows success/error message
- Clears deadline on success

### 6. Resend Verification API

Add to existing page or create endpoint:
- Check cooldown (5 minutes between resends)
- Generate new token
- Send new email
- Update `EmailVerificationSentAt`

### 7. Account Deactivation Job

Create `EmailVerificationEnforcementJob.cs`:
- Runs daily via Hangfire recurring job
- Finds users where:
  - `EmailConfirmed == false`
  - `EmailVerificationDeadline < DateTime.UtcNow`
  - `IsDeactivated == false`
  - NOT in `AdminEmails` config (admins exempt)
- For each user:
  - Sets `IsDeactivated = true`, `DeactivationReason = "EmailNotVerified"`
  - **Disables all linked Rivian account WebSocket subscriptions**
  - Clears Rivian account tokens (user must re-link after reactivation)
  - Sends "Account deactivated" email

### 8. Warning Email Job

Create `EmailVerificationReminderJob.cs`:
- Runs hourly via Hangfire recurring job
- Finds users where:
  - `EmailConfirmed == false`
  - `EmailVerificationDeadline` is within next 24 hours
  - `IsDeactivated == false`
  - NOT in `AdminEmails` config
  - Haven't already received reminder (track with new field or check email log)
- Sends "Your account will be deactivated in 24 hours" warning email

### 9. Login Flow Changes

**Login.razor** updates:
- After successful auth, check `IsDeactivated`
- If deactivated, show message with option to resend verification
- Don't allow login to dashboard if deactivated

### 10. Reactivation Flow

When a deactivated user verifies their email:
- Clear `IsDeactivated` flag
- Clear `DeactivationReason`
- Allow login again
- Dashboard shows "Link Rivian Account" prompt (tokens were cleared)
- User must re-authenticate with Rivian to resume data collection

---

## User Flows

### New User Registration
```
Register → Send verification email → Dashboard (with banner)
                                           ↓
                              [7 days pass without verification]
                                           ↓
                    24-hour warning email → Account deactivated → Login blocked
                                                                      ↓
                                           User clicks verify link (or requests new one)
                                                                      ↓
                                           Account reactivated → Must re-link Rivian account
```

### Existing User (No Email Verified)
```
Login → Dashboard shows verification banner
              ↓
        Click "Resend verification email"
              ↓
        Check email → Click link → Verified → Banner disappears
```

### Deactivated User Tries to Login
```
Login attempt → "Account deactivated" message
                      ↓
              "Resend verification email" option
                      ↓
              Verify email → Account reactivated → Login succeeds
                      ↓
              Dashboard prompts to re-link Rivian account
```

---

## Files to Create/Modify

### New Files
| File | Purpose |
|------|---------|
| `Email/Templates/EmailVerification.html` | Verification email template |
| `Email/Templates/EmailVerificationReminder.html` | 2-day warning template |
| `Email/Templates/AccountDeactivated.html` | Deactivation notice template |
| `Components/Shared/EmailVerificationBanner.razor` | Dashboard notification |
| `Components/Pages/Account/VerifyEmail.razor` | Verification landing page |
| `Services/Email/EmailVerificationEnforcementJob.cs` | Daily deactivation job |
| `Services/Email/EmailVerificationReminderJob.cs` | Daily reminder job |

### Modified Files
| File | Changes |
|------|---------|
| `Core/Entities/ApplicationUser.cs` | Add new fields |
| `Components/Pages/Account/Register.razor` | Send verification email |
| `Components/Pages/Account/Login.razor` | Check deactivation status |
| `Components/Pages/Dashboard.razor` | Include verification banner |
| `Services/Email/EmailTriggerService.cs` | Add verification helper methods |
| `Program.cs` | Register recurring Hangfire jobs |

### Migration
```
AddEmailVerificationFields
- EmailVerificationSentAt (nullable DateTime)
- EmailVerificationDeadline (nullable DateTime)
- IsDeactivated (bool, default false)
- DeactivationReason (nullable string)
```

---

## Configuration

Add to `appsettings.json`:

```json
{
  "RivianMate": {
    "EmailVerification": {
      "Required": true,
      "GracePeriodDays": 7,
      "ReminderHoursBeforeDeadline": 24,
      "ResendCooldownMinutes": 5
    }
  }
}
```

---

## Edge Cases

1. **User changes email**: Reset verification status, send new verification email, reset deadline
2. **Email system disabled**: Skip all verification logic, don't show banner
3. **Existing users**: Set deadline to 7 days from when feature is deployed (migration sets default)
4. **Admin users**: Exempt from verification requirements (checked against `AdminEmails` config)
5. **User deletes account**: Normal deletion, no special handling needed
6. **Reactivation**: User must re-link Rivian account after verifying email (tokens cleared on deactivation)
7. **Multiple Rivian accounts**: All linked accounts disabled on deactivation, all must be re-linked

---

## Implementation Order

1. **Phase 1: Core Infrastructure**
   - Add database fields + migration
   - Create EmailVerification.html template
   - Create VerifyEmail.razor page
   - Add `FireEmailVerification()` to trigger service

2. **Phase 2: Registration Integration**
   - Update Register.razor to send verification email
   - Set deadline on registration

3. **Phase 3: Dashboard Banner**
   - Create EmailVerificationBanner.razor
   - Add to Dashboard.razor
   - Implement resend functionality with cooldown

4. **Phase 4: Enforcement**
   - Create deactivation job
   - Update login to check deactivation
   - Create AccountDeactivated.html template

5. **Phase 5: Polish**
   - Create reminder email + job
   - Handle edge cases
   - Add configuration options

---

## Decisions

1. **Admins exempt**: Yes - admin users (in `AdminEmails` config) skip verification requirements
2. **Deactivated access**: Fully blocked - no dashboard access, login shows deactivation message
3. **Polling on deactivation**: Disable WebSocket subscriptions for all linked Rivian accounts. User must re-link after reactivation.
4. **Reminder timing**: 24 hours before deactivation deadline
