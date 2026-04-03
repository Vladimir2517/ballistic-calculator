"""
Core ballistic physics engine.
Uses point-mass trajectory model with drag (G1 or G7 BC).
"""

import math
from dataclasses import dataclass, field
from typing import Optional


# Standard atmosphere constants (ISA sea level)
RHO0 = 1.2250        # kg/m³  — air density at sea level
G    = 9.80665       # m/s²   — standard gravity
R    = 287.058       # J/(kg·K)

# Mach 1 at standard conditions (~15 °C)
MACH1_STD = 343.0    # m/s


@dataclass
class AtmosphericConditions:
    temperature_c: float = 15.0   # °C
    pressure_hpa: float = 1013.25 # hPa
    humidity_pct: float = 50.0    # %
    altitude_m:   float = 0.0     # m above sea level

    def air_density(self) -> float:
        """Compute actual air density (kg/m³)."""
        T_k = self.temperature_c + 273.15
        p   = self.pressure_hpa * 100.0
        # Partial pressure of water vapour (Magnus formula)
        pv  = self.humidity_pct / 100.0 * 6.1078e2 * math.exp(
              17.27 * self.temperature_c / (self.temperature_c + 237.3))
        pd  = p - pv
        rho = (pd / (287.058 * T_k)) + (pv / (461.495 * T_k))
        return rho

    def speed_of_sound(self) -> float:
        """Speed of sound (m/s) at current temperature."""
        T_k = self.temperature_c + 273.15
        return math.sqrt(1.4 * R * T_k)


@dataclass
class Bullet:
    """Bullet / projectile parameters."""
    name:          str
    mass_g:        float          # grams
    diameter_mm:   float          # mm
    bc_g1:         float = 0.0    # Ballistic Coefficient (G1 model)
    bc_g7:         float = 0.0    # Ballistic Coefficient (G7 model)
    muzzle_vel_ms: float = 900.0  # m/s

    @property
    def mass_kg(self) -> float:
        return self.mass_g / 1000.0

    @property
    def diameter_m(self) -> float:
        return self.diameter_mm / 1000.0

    @property
    def sectional_density(self) -> float:
        area = math.pi * (self.diameter_m / 2) ** 2
        return self.mass_kg / area  # kg/m²


@dataclass
class TrajectoryPoint:
    time_s:     float
    x_m:        float   # downrange distance
    y_m:        float   # height (+ = up)
    vx_ms:      float   # horizontal velocity
    vy_ms:      float   # vertical velocity
    velocity_ms: float
    mach:       float
    energy_j:   float
    drift_m:    float = 0.0   # horizontal wind drift


def _cd_g1(mach: float) -> float:
    """
    G1 drag coefficient as a function of Mach number.
    Piecewise approximation of the ICAO/Ingalls drag function.
    """
    m = mach
    if m < 0.8:
        cd = 0.2656 - 0.1199 * m + 0.0961 * m ** 2
    elif m < 1.0:
        cd = 0.2656 + (m - 0.8) / 0.2 * 0.17
    elif m < 1.2:
        cd = 0.4368 - 0.1104 * (m - 1.0)
    elif m < 2.0:
        cd = 0.4258 * math.exp(-0.55 * (m - 1.2))
    else:
        cd = 0.2050 * m ** (-0.5)
    return max(cd, 0.05)


def _cd_g7(mach: float) -> float:
    """
    G7 drag coefficient (long-range boat-tail bullets).
    """
    m = mach
    if m < 0.8:
        cd = 0.1198
    elif m < 1.0:
        cd = 0.1198 + (m - 0.8) / 0.2 * 0.12
    elif m < 1.2:
        cd = 0.2398 - 0.08 * (m - 1.0)
    elif m < 2.0:
        cd = 0.2238 * math.exp(-0.45 * (m - 1.2))
    else:
        cd = 0.1350 * m ** (-0.45)
    return max(cd, 0.025)


def compute_trajectory(
    bullet:       Bullet,
    atmo:         AtmosphericConditions,
    elevation_mrad: float   = 0.0,   # mrad elevation angle
    zero_range_m:  float    = 100.0, # zeroing distance (m)
    max_range_m:   float    = 1500.0,
    step_m:        float    = 25.0,  # output step (m)
    wind_ms:       float    = 0.0,   # cross-wind m/s (+ = right)
    use_g7:        bool     = False,
) -> list[TrajectoryPoint]:
    """
    Integrate bullet trajectory using RK4 with atmospheric drag.

    Returns a list of TrajectoryPoint sampled every `step_m` metres.
    """
    rho   = atmo.air_density()
    c_snd = atmo.speed_of_sound()

    bc    = bullet.bc_g7 if (use_g7 and bullet.bc_g7 > 0) else bullet.bc_g1
    if bc <= 0:
        bc = 0.3  # fallback generic BC

    cd_fn = _cd_g7 if (use_g7 and bullet.bc_g7 > 0) else _cd_g1

    # Bullet reference area
    area = math.pi * (bullet.diameter_m / 2) ** 2

    # Elevation angle (positive = up)
    elev_rad = elevation_mrad * 1e-3

    # Initial state
    v0x = bullet.muzzle_vel_ms * math.cos(elev_rad)
    v0y = bullet.muzzle_vel_ms * math.sin(elev_rad)

    t, x, y = 0.0, 0.0, 0.0
    vx, vy  = v0x, v0y
    vz      = 0.0   # cross-range (wind drift direction)
    z       = 0.0

    results: list[TrajectoryPoint] = []
    next_x  = 0.0

    def accel(vx_, vy_, vz_):
        v_total = math.sqrt(vx_ ** 2 + vy_ ** 2 + vz_ ** 2)
        mach_   = v_total / c_snd
        cd_     = cd_fn(mach_) / bc  # scale by BC
        # drag deceleration magnitude
        Fd_over_m = 0.5 * rho * v_total ** 2 * cd_ * area / bullet.mass_kg
        ax = -Fd_over_m * vx_ / v_total if v_total > 0 else 0
        ay = -Fd_over_m * vy_ / v_total - G if v_total > 0 else -G
        az = -Fd_over_m * (vz_ - wind_ms) / v_total if v_total > 0 else 0
        return ax, ay, az

    dt = 0.001  # 1 ms integration step

    while x <= max_range_m and y > -500:
        # Sample at downrange grid points
        if x >= next_x:
            v_mag  = math.sqrt(vx ** 2 + vy ** 2 + vz ** 2)
            mach_v = v_mag / c_snd
            e_j    = 0.5 * bullet.mass_kg * v_mag ** 2
            results.append(TrajectoryPoint(
                time_s=t, x_m=x, y_m=y,
                vx_ms=vx, vy_ms=vy,
                velocity_ms=v_mag,
                mach=mach_v,
                energy_j=e_j,
                drift_m=z,
            ))
            next_x += step_m

        # RK4 integration
        ax1, ay1, az1 = accel(vx, vy, vz)
        ax2, ay2, az2 = accel(vx + 0.5*dt*ax1, vy + 0.5*dt*ay1, vz + 0.5*dt*az1)
        ax3, ay3, az3 = accel(vx + 0.5*dt*ax2, vy + 0.5*dt*ay2, vz + 0.5*dt*az2)
        ax4, ay4, az4 = accel(vx + dt*ax3,      vy + dt*ay3,     vz + dt*az3)

        vx += dt * (ax1 + 2*ax2 + 2*ax3 + ax4) / 6
        vy += dt * (ay1 + 2*ay2 + 2*ay3 + ay4) / 6
        vz += dt * (az1 + 2*az2 + 2*az3 + az4) / 6

        x  += vx * dt
        y  += vy * dt
        z  += vz * dt
        t  += dt

    return results


def find_zero_elevation(
    bullet: Bullet,
    atmo:   AtmosphericConditions,
    zero_range_m: float = 100.0,
    use_g7: bool = False,
    tol_mm: float = 1.0,
) -> float:
    """
    Binary-search the elevation (mrad) that makes the bullet cross y=0
    at exactly `zero_range_m`.  Returns the elevation in mrad.
    """
    lo, hi = -5.0, 30.0  # mrad search range
    for _ in range(60):
        mid = (lo + hi) / 2
        pts = compute_trajectory(bullet, atmo,
                                 elevation_mrad=mid,
                                 max_range_m=zero_range_m + 1,
                                 step_m=zero_range_m,
                                 use_g7=use_g7)
        # find y at zero_range_m
        y_at_zero = None
        for p in pts:
            if abs(p.x_m - zero_range_m) < 2.0:
                y_at_zero = p.y_m
                break
        if y_at_zero is None:
            break
        if abs(y_at_zero * 1000) < tol_mm:
            return mid
        if y_at_zero < 0:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


def drop_table(
    bullet:       Bullet,
    atmo:         AtmosphericConditions,
    zero_range_m: float = 100.0,
    max_range_m:  float = 1000.0,
    step_m:       float = 50.0,
    wind_ms:      float = 0.0,
    use_g7:       bool  = False,
) -> list[dict]:
    """
    Produce a ballistic drop table zeroed at `zero_range_m`.

    Returns list of dicts with drop (mm), drift (mm), velocity, energy, TOF.
    """
    elev = find_zero_elevation(bullet, atmo, zero_range_m, use_g7)
    pts  = compute_trajectory(bullet, atmo,
                              elevation_mrad=elev,
                              max_range_m=max_range_m,
                              step_m=step_m,
                              wind_ms=wind_ms,
                              use_g7=use_g7)
    # y at zero to subtract
    y_zero = 0.0
    for p in pts:
        if abs(p.x_m - zero_range_m) <= step_m / 2:
            y_zero = p.y_m
            break

    rows = []
    for p in pts:
        rows.append({
            "range_m":    round(p.x_m),
            "drop_mm":    round((p.y_m - y_zero) * 1000, 1),
            "drift_mm":   round(p.drift_m * 1000, 1),
            "velocity_ms": round(p.velocity_ms, 1),
            "energy_j":   round(p.energy_j, 1),
            "tof_s":      round(p.time_s, 4),
            "mach":       round(p.mach, 3),
        })
    return rows
