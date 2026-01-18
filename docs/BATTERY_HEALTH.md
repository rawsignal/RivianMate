# RivianMate Battery Health Estimation

## Overview

RivianMate uses the `batteryCapacity` field from the Rivian API to directly measure battery health. This is significantly more accurate than previous approaches that relied on estimating capacity from charging sessions or range projections.

## How It Works

### 1. Data Collection

The Rivian API exposes a `batteryCapacity` field in the vehicle state response. This field reports the **current usable battery capacity in kWh**.

```graphql
batteryCapacity { timeStamp value }
```

Forum observations show this value:
- Reports to 6 decimal places (e.g., `130.774994`)
- Fluctuates slightly based on temperature and state of charge
- Decreases over time as the battery degrades

### 2. Reference Capacities (When New)

We compare the reported capacity against known original capacities from Rivian's official specs:

#### Gen 1 Vehicles (2022-2024)
| Pack Type | Original Capacity |
|-----------|------------------|
| Standard  | 106.0 kWh        |
| Large     | 131.0 kWh        |
| Max       | 141.0 kWh        |

#### Gen 2 Vehicles (2025+)
| Pack Type | Original Capacity |
|-----------|------------------|
| Standard  | 92.5 kWh         |
| Large     | 108.5 kWh        |
| Max       | 140.0 kWh        |

### 3. Health Calculation

```
Health % = (Reported Capacity / Original Capacity) × 100
```

Example: A Gen 1 Large pack reporting 128.35 kWh:
```
Health = (128.35 / 131.0) × 100 = 97.9%
```

### 4. Trend Analysis

By tracking `batteryCapacity` readings over time, we calculate:

- **Degradation Rate**: Percentage points lost per 10,000 miles
- **Projections**: Estimated health at 100k miles, 150k miles
- **Warranty Threshold**: Miles until 70% capacity (warranty limit)

We use linear regression on historical snapshots to determine the degradation trend.

### 5. Data Quality Considerations

The system tracks data quality factors:

- **State of Charge**: Readings may vary slightly at different SoC levels
- **Temperature**: Cold batteries may report lower capacity
- **Calibration**: The BMS recalibrates periodically, causing slight jumps

We store these factors with each snapshot to enable future analysis refinements.

## Implementation

### Key Files

- `BatteryPackSpecs.cs` - Reference data for original capacities
- `BatteryHealthService.cs` - Calculates health and stores snapshots
- `BatteryHealthSnapshot.cs` - Entity for storing health records
- `VehicleState.cs` - Includes `BatteryCapacityKwh` field
- `RivianApiModels.cs` - API response model with `BatteryCapacity`

### API Query

The GraphQL query includes:
```graphql
batteryCapacity { timeStamp value }
```

### Recording Strategy

Health snapshots are recorded:
- At most once per hour
- When capacity changes by more than 0.5 kWh
- Only when the API returns valid capacity data

## Rivian Warranty Reference

- Battery warranted to 70% capacity
- 8 years or 175,000 miles (whichever comes first)
- The system tracks progress toward this threshold

## Advantages Over Previous Approaches

| Approach | Accuracy | Complexity |
|----------|----------|------------|
| **batteryCapacity field** | Excellent | Simple |
| Charging session analysis | Good | Complex |
| Range projection | Fair | Simple |

Using `batteryCapacity` directly is superior because:
1. No need to calculate from indirect data
2. Less affected by driving style variations
3. Immediate readings (no need to wait for charging)
4. Already calibrated by Rivian's BMS
