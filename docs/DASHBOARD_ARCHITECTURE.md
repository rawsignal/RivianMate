# Dashboard Architecture

## Overview

RivianMate's dashboard is built around a **Feature + Card** architecture where:

- **Features** are complete functional modules (Battery Health, Charging, Drives, etc.)
- **Cards** are configurable summary widgets that surface key data from features
- **Users** can customize their dashboard by selecting, ordering, and configuring cards

---

## Core Concepts

### Features

A **Feature** is a self-contained module that:
- Has a dedicated detail page with comprehensive information
- Exposes one or more **metrics** that can be displayed on cards
- Defines what data points are available for card customization
- Handles its own data fetching, calculations, and visualization

```
Feature
├── Detail Page (full feature UI)
├── Available Metrics (data points for cards)
├── Card Component (summary widget)
└── Settings (feature-specific config)
```

### Cards

A **Card** is a dashboard widget that:
- Belongs to exactly one Feature
- Displays a subset of the feature's metrics
- Links to the feature's detail page when clicked
- Can be configured by the user (within limits)
- Has size variants (small, medium, large)

### Metrics

A **Metric** is a single displayable data point:
- Has a name, value, unit, and optional icon
- Can be primary (large display) or secondary (supporting info)
- May include trend indicators, sparklines, or status badges

---

## Feature Registry

### Built-in Features

| Feature | Description | Default Card Metrics |
|---------|-------------|---------------------|
| **Battery Health** | Degradation tracking, projections, warranty status | Health %, Current Capacity, Trend |
| **State of Charge** | Current battery level, charging status | SoC %, Range, Charging State |
| **Charging Sessions** | History, costs, statistics | Recent sessions, Total energy, Avg cost |
| **Drives** | Trip history, efficiency, routes | Recent drives, Total miles, Avg efficiency |
| **Location** | Current position, geofencing | Map preview, Address, Last moved |
| **Climate** | Cabin temp, preconditioning | Interior temp, Climate status |
| **Vehicle Status** | Doors, windows, locks, tires | Lock state, Open items count |
| **Software** | OTA updates, version history | Current version, Update available |
| **Efficiency** | Energy consumption analysis | mi/kWh, Trends, Comparisons |

### Feature Definition Schema

```csharp
public class FeatureDefinition
{
    public string Id { get; set; }              // "battery-health"
    public string Name { get; set; }            // "Battery Health"
    public string Description { get; set; }
    public string Icon { get; set; }            // Lucide icon name
    public string DetailPageRoute { get; set; } // "/battery-health"
    public List<MetricDefinition> AvailableMetrics { get; set; }
    public CardConfiguration DefaultCardConfig { get; set; }
    public bool IsProFeature { get; set; }      // Requires Pro license
}

public class MetricDefinition
{
    public string Id { get; set; }              // "health-percent"
    public string Name { get; set; }            // "Health Percentage"
    public string Unit { get; set; }            // "%"
    public MetricDisplayType DisplayType { get; set; }  // Number, Progress, Sparkline, Status
    public bool CanBePrimary { get; set; }      // Can be the main/hero metric
    public string Icon { get; set; }            // Optional icon
    public int MaxDecimalPlaces { get; set; }
}

public enum MetricDisplayType
{
    Number,         // Plain number with unit
    Progress,       // Progress bar (0-100)
    Sparkline,      // Mini trend chart
    Status,         // Badge/pill with status text
    Temperature,    // Temp with color coding
    Duration,       // Time display
    Distance,       // Miles/km with unit
    Currency        // Money with formatting
}
```

---

## Card Configuration

### Card Structure

Each card has:
1. **Primary Metric** - The hero number/visualization (required)
2. **Secondary Metrics** - Supporting data points (0-3 items)
3. **Size** - Small (1x1), Medium (2x1), Large (2x2)
4. **Title Override** - Optional custom title

```csharp
public class CardConfiguration
{
    public string FeatureId { get; set; }
    public string PrimaryMetricId { get; set; }
    public List<string> SecondaryMetricIds { get; set; }  // Max 3
    public CardSize Size { get; set; }
    public string? TitleOverride { get; set; }
    public int Order { get; set; }  // Position in dashboard
}

public enum CardSize
{
    Small,   // 1 column, compact
    Medium,  // 2 columns, standard
    Large    // 2 columns, expanded with more detail
}
```

### Size Constraints

| Size | Primary | Secondary | Notes |
|------|---------|-----------|-------|
| Small | 1 metric | 0 | Number + label only |
| Medium | 1 metric | 1-2 | Standard card with subtext |
| Large | 1 metric | 2-3 | Room for sparklines, extra context |

---

## User Dashboard State

### Storage Schema

```csharp
public class UserDashboardConfig
{
    public Guid UserId { get; set; }
    public Guid VehicleId { get; set; }  // Dashboard is per-vehicle
    public List<CardConfiguration> Cards { get; set; }
    public DateTime LastModified { get; set; }
}
```

### Database Table

```sql
CREATE TABLE dashboard_configs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL,
    vehicle_id UUID NOT NULL REFERENCES vehicles(id),
    config JSONB NOT NULL,  -- Serialized UserDashboardConfig
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(user_id, vehicle_id)
);
```

---

## Default Dashboard

New users get a sensible default:

```
┌─────────────────────────────────────────────────────────────────┐
│  [Medium] State of Charge    [Medium] Battery Health            │
│  73% · 234 mi range          97.2% · 128.4 kWh                  │
├─────────────────────────────────────────────────────────────────┤
│  [Large] Recent Charging                                        │
│  Last 4 sessions with energy, duration, location                │
├─────────────────────────────────────────────────────────────────┤
│  [Small] Odometer   [Small] Efficiency   [Small] Climate        │
│  12,456 mi          2.4 mi/kWh           68°F                   │
├─────────────────────────────────────────────────────────────────┤
│  [Medium] Vehicle Status     [Medium] Software                  │
│  Locked · All closed         2024.51.02 · Up to date            │
└─────────────────────────────────────────────────────────────────┘
```

---

## Card Customization UI

### Adding a Card

1. User clicks "+ Add Card" button
2. Modal shows available features (grouped by category)
3. User selects a feature
4. Preview shows default card configuration
5. User can customize:
   - Primary metric (dropdown of available metrics)
   - Secondary metrics (multi-select, max based on size)
   - Card size (affects available secondary slots)
6. User confirms, card appears at end of dashboard

### Editing a Card

1. User clicks card menu (⋮) or enters edit mode
2. Options:
   - **Configure** - Opens same UI as adding, pre-filled
   - **Resize** - Quick toggle between sizes
   - **Remove** - Removes from dashboard (feature still available)

### Reordering Cards

- Drag-and-drop in edit mode
- Cards snap to grid
- Order persisted to database

### Edit Mode

- Toggle via "Edit Dashboard" button
- All cards show drag handles and menu buttons
- "+ Add Card" placeholder appears
- "Done" button exits edit mode and saves

---

## Implementation Plan

### Phase 1: Core Infrastructure
- [ ] Create `FeatureDefinition` and `MetricDefinition` models
- [ ] Build feature registry with all built-in features
- [ ] Create `CardConfiguration` and `UserDashboardConfig` models
- [ ] Add database migration for `dashboard_configs` table
- [ ] Build `DashboardService` for CRUD operations

### Phase 2: Card Components
- [ ] Create base `DashboardCard` Blazor component
- [ ] Implement size variants (Small, Medium, Large)
- [ ] Build metric display components for each `MetricDisplayType`
- [ ] Add click-to-navigate functionality
- [ ] Implement loading and error states

### Phase 3: Dashboard Layout
- [ ] Create responsive grid layout component
- [ ] Implement card positioning logic
- [ ] Handle different screen sizes
- [ ] Add smooth animations for card changes

### Phase 4: Customization UI
- [ ] Build "Add Card" modal with feature browser
- [ ] Create card configuration form
- [ ] Implement drag-and-drop reordering
- [ ] Add edit mode toggle
- [ ] Build card context menu

### Phase 5: Persistence
- [ ] Save dashboard config on changes
- [ ] Load user's config on dashboard mount
- [ ] Handle default dashboard for new users
- [ ] Sync across devices (if user is logged in)

---

## Example: Battery Health Feature

### Feature Definition

```csharp
new FeatureDefinition
{
    Id = "battery-health",
    Name = "Battery Health",
    Description = "Track battery degradation and warranty status",
    Icon = "battery-full",
    DetailPageRoute = "/battery-health",
    AvailableMetrics = new List<MetricDefinition>
    {
        new() { 
            Id = "health-percent", 
            Name = "Health", 
            Unit = "%", 
            DisplayType = MetricDisplayType.Progress,
            CanBePrimary = true 
        },
        new() { 
            Id = "current-capacity", 
            Name = "Current Capacity", 
            Unit = "kWh", 
            DisplayType = MetricDisplayType.Number,
            CanBePrimary = true,
            MaxDecimalPlaces = 1
        },
        new() { 
            Id = "original-capacity", 
            Name = "Original Capacity", 
            Unit = "kWh", 
            DisplayType = MetricDisplayType.Number,
            CanBePrimary = false 
        },
        new() { 
            Id = "capacity-lost", 
            Name = "Capacity Lost", 
            Unit = "kWh", 
            DisplayType = MetricDisplayType.Number,
            CanBePrimary = false,
            MaxDecimalPlaces = 1
        },
        new() { 
            Id = "degradation-rate", 
            Name = "Degradation Rate", 
            Unit = "% / 10k mi", 
            DisplayType = MetricDisplayType.Number,
            CanBePrimary = false 
        },
        new() { 
            Id = "health-trend", 
            Name = "Health Trend", 
            Unit = "", 
            DisplayType = MetricDisplayType.Sparkline,
            CanBePrimary = false 
        },
        new() { 
            Id = "warranty-status", 
            Name = "Warranty Status", 
            Unit = "", 
            DisplayType = MetricDisplayType.Status,
            CanBePrimary = false 
        },
        new() { 
            Id = "projected-100k", 
            Name = "Projected at 100k mi", 
            Unit = "%", 
            DisplayType = MetricDisplayType.Number,
            CanBePrimary = false 
        },
    },
    DefaultCardConfig = new CardConfiguration
    {
        PrimaryMetricId = "health-percent",
        SecondaryMetricIds = new[] { "current-capacity", "degradation-rate" },
        Size = CardSize.Medium
    }
}
```

### Card Rendering Examples

**Small Card:**
```
┌─────────────────┐
│ ⚡ BATTERY HEALTH
│     97.2%       
└─────────────────┘
```

**Medium Card (Default):**
```
┌─────────────────────────────────┐
│ ⚡ BATTERY HEALTH               │
│                                 │
│     97.2%                       │
│     ━━━━━━━━━━━━━━━━━━━━━━━━━  │
│                                 │
│ 128.4 kWh  ·  0.8% / 10k mi    │
└─────────────────────────────────┘
```

**Large Card:**
```
┌─────────────────────────────────────────────────────┐
│ ⚡ BATTERY HEALTH                      View Details →│
│                                                     │
│     97.2%              [Sparkline chart]            │
│     ━━━━━━━━━━━━━━━    ╱╲  ╱╲                      │
│                       ╱  ╲╱  ╲____                  │
│                                                     │
│ Current: 128.4 kWh  ·  Original: 131 kWh           │
│ Rate: 0.8% / 10k mi  ·  Warranty: ✓ Covered        │
└─────────────────────────────────────────────────────┘
```

---

## Data Flow

```
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│                  │     │                  │     │                  │
│  Rivian API      │────▶│  Feature Service │────▶│  Metric Values   │
│  (Polling)       │     │  (Calculations)  │     │  (Current Data)  │
│                  │     │                  │     │                  │
└──────────────────┘     └──────────────────┘     └────────┬─────────┘
                                                           │
                                                           ▼
┌──────────────────┐     ┌──────────────────┐     ┌──────────────────┐
│                  │     │                  │     │                  │
│  Dashboard       │◀────│  Card            │◀────│  Card Config     │
│  (Grid Layout)   │     │  Components      │     │  (User Prefs)    │
│                  │     │                  │     │                  │
└──────────────────┘     └──────────────────┘     └──────────────────┘
```

1. **Polling Service** fetches data from Rivian API
2. **Feature Services** process raw data into meaningful metrics
3. **Card Components** subscribe to metric values they need
4. **Dashboard** renders cards based on user's configuration
5. **Real-time updates** flow through SignalR to connected clients

---

## Future Considerations

### Custom Cards (Pro Feature?)
- Let users create cards with custom metric combinations
- Support for calculated/derived metrics
- Custom titles and icons

### Card Templates
- Pre-built card configurations for common use cases
- "Charging focused" template, "Road trip" template, etc.

### Dashboard Sharing
- Export/import dashboard configurations
- Community-shared templates

### Widgets
- Condensed cards for mobile home screen widgets
- Apple Watch / Wear OS complications
