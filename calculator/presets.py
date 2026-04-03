"""
Pre-defined ammunition / calibre presets.
"""

from .ballistics import Bullet

PRESETS: dict[str, Bullet] = {
    # ──── Small arms ────────────────────────────────────────────────
    "5.45x39 7N6": Bullet(
        name="5.45×39 мм 7Н6",
        mass_g=3.45,
        diameter_mm=5.60,
        bc_g1=0.168,
        bc_g7=0.085,
        muzzle_vel_ms=900.0,
    ),
    "5.56x45 M855": Bullet(
        name="5.56×45 мм М855 (SS109)",
        mass_g=4.02,
        diameter_mm=5.70,
        bc_g1=0.307,
        bc_g7=0.151,
        muzzle_vel_ms=945.0,
    ),
    "7.62x39 M43": Bullet(
        name="7.62×39 мм M43 (АК)",
        mass_g=7.97,
        diameter_mm=7.92,
        bc_g1=0.275,
        bc_g7=0.130,
        muzzle_vel_ms=715.0,
    ),
    "7.62x54R B32": Bullet(
        name="7.62×54R Б-32 (ПКМ/СВД)",
        mass_g=9.65,
        diameter_mm=7.92,
        bc_g1=0.400,
        bc_g7=0.205,
        muzzle_vel_ms=855.0,
    ),
    # ──── Sniper ─────────────────────────────────────────────────────
    ".308 Win 175gr Sierra": Bullet(
        name=".308 Win 175 гр Sierra MatchKing",
        mass_g=11.34,
        diameter_mm=7.82,
        bc_g1=0.505,
        bc_g7=0.258,
        muzzle_vel_ms=805.0,
    ),
    ".338 Lapua 250gr": Bullet(
        name=".338 Lapua Magnum 250 гр SCENAR",
        mass_g=16.2,
        diameter_mm=8.60,
        bc_g1=0.640,
        bc_g7=0.330,
        muzzle_vel_ms=905.0,
    ),
    # ──── Heavy MG / Anti-materiel ──────────────────────────────────
    "12.7x108 B32": Bullet(
        name="12.7×108 мм Б-32 (НСВ/КПВТ)",
        mass_g=48.3,
        diameter_mm=13.00,
        bc_g1=0.900,
        bc_g7=0.460,
        muzzle_vel_ms=860.0,
    ),
    ".50 BMG M33": Bullet(
        name=".50 BMG M33 Ball",
        mass_g=42.0,
        diameter_mm=12.95,
        bc_g1=0.670,
        bc_g7=0.340,
        muzzle_vel_ms=887.0,
    ),
    # ──── Artillery (simplified point-mass) ─────────────────────────
    "82mm mortar OF-832": Bullet(
        name="82 мм міна ОФ-832",
        mass_g=3_230.0,
        diameter_mm=82.0,
        bc_g1=2.8,
        bc_g7=0.0,
        muzzle_vel_ms=211.0,
    ),
    "120mm mortar OF-843": Bullet(
        name="120 мм міна ОФ-843",
        mass_g=16_000.0,
        diameter_mm=120.0,
        bc_g1=5.5,
        bc_g7=0.0,
        muzzle_vel_ms=272.0,
    ),
}


def list_presets() -> list[str]:
    return list(PRESETS.keys())


def get_preset(name: str) -> Bullet:
    if name not in PRESETS:
        raise KeyError(f"Пресет '{name}' не знайдено. Доступні: {list_presets()}")
    return PRESETS[name]
