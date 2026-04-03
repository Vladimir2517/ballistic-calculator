"""
Wind correction utilities.
"""

import math


def wind_component(speed_ms: float, direction_deg: float, heading_deg: float = 0.0) -> float:
    """
    Compute cross-wind component (m/s) perpendicular to bullet's heading.

    Args:
        speed_ms:      Wind speed in m/s.
        direction_deg: Meteorological wind direction (degrees FROM which wind blows, 0=N).
        heading_deg:   Shooter's azimuth (degrees, 0=N).

    Returns:
        Cross-wind (m/s): positive = wind from left (bullet drifts right).
    """
    # Convert met direction to vector direction (where wind IS going)
    wind_vec_deg = (direction_deg + 180) % 360
    # Angle between bullet heading and wind vector
    delta = math.radians(wind_vec_deg - heading_deg)
    return speed_ms * math.sin(delta)


def beaufort_to_ms(beaufort: int) -> float:
    """Convert Beaufort scale to approximate m/s (midpoint)."""
    table = [0.3, 1.5, 3.3, 5.5, 8.0, 10.8, 13.9, 17.2, 20.8, 24.5, 28.5, 32.7, 37.0]
    if beaufort < 0 or beaufort > 12:
        raise ValueError("Шкала Бофорта: 0–12")
    return table[beaufort]


def moa_to_clicks(moa: float, click_value_moa: float = 0.25) -> float:
    """Convert angular correction (MOA) to scope clicks."""
    return moa / click_value_moa


def mrad_to_moa(mrad: float) -> float:
    return mrad * (180.0 / math.pi * 60.0 / 1000.0)


def moa_to_mrad(moa: float) -> float:
    return moa / (180.0 / math.pi * 60.0 / 1000.0)


def correction_mrad(drift_m: float, range_m: float) -> float:
    """Angular correction in mrad for observed drift at given range."""
    if range_m <= 0:
        return 0.0
    return math.atan2(drift_m, range_m) * 1000.0
