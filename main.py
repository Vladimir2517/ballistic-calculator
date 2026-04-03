#!/usr/bin/env python3
"""
Ballistic Calculator — головний інтерфейс командного рядка.

Використання:
  python main.py table   --preset "7.62x54R B32" --zero 300 --range 1000 --wind 5
  python main.py custom  --mass 9.65 --diameter 7.92 --bc 0.40 --vel 855 --zero 300
  python main.py presets
  python main.py traj    --preset ".308 Win 175gr Sierra" --elev 5.2
"""

import argparse
import sys
from tabulate import tabulate

from calculator.ballistics import (
    Bullet, AtmosphericConditions,
    drop_table, compute_trajectory, find_zero_elevation,
)
from calculator.presets import get_preset, list_presets
from calculator.wind import (
    wind_component, beaufort_to_ms,
    correction_mrad, mrad_to_moa, moa_to_clicks,
)


# ── Helpers ──────────────────────────────────────────────────────────────────

def build_atmo(args) -> AtmosphericConditions:
    return AtmosphericConditions(
        temperature_c=getattr(args, "temp",     15.0) or 15.0,
        pressure_hpa= getattr(args, "pressure", 1013.25) or 1013.25,
        humidity_pct= getattr(args, "humidity", 50.0) or 50.0,
        altitude_m=   getattr(args, "altitude", 0.0) or 0.0,
    )


def build_bullet_from_args(args) -> Bullet:
    return Bullet(
        name=f"Custom {args.diameter}×{args.mass}g",
        mass_g=args.mass,
        diameter_mm=args.diameter,
        bc_g1=args.bc,
        bc_g7=getattr(args, "bc_g7", 0.0) or 0.0,
        muzzle_vel_ms=args.vel,
    )


def print_header(title: str) -> None:
    print()
    print("═" * 72)
    print(f"  {title}")
    print("═" * 72)


# ── Subcommands ──────────────────────────────────────────────────────────────

def cmd_presets(_args) -> None:
    print_header("Доступні пресети боєприпасів")
    for key in list_presets():
        b = get_preset(key)
        print(f"  {key:<30}  BC(G1)={b.bc_g1:.3f}  V₀={b.muzzle_vel_ms} м/с  "
              f"маса={b.mass_g} г  ∅{b.diameter_mm} мм")
    print()


def cmd_table(args) -> None:
    if args.preset:
        bullet = get_preset(args.preset)
    else:
        bullet = build_bullet_from_args(args)

    atmo = build_atmo(args)

    cross = 0.0
    if args.wind:
        if args.wind_dir is not None:
            cross = wind_component(args.wind, args.wind_dir,
                                   getattr(args, "heading", 0.0) or 0.0)
        else:
            cross = args.wind   # assume full cross-wind

    use_g7 = getattr(args, "g7", False)

    rows = drop_table(
        bullet       = bullet,
        atmo         = atmo,
        zero_range_m = args.zero,
        max_range_m  = args.range,
        step_m       = getattr(args, "step", 50) or 50,
        wind_ms      = cross,
        use_g7       = use_g7,
    )

    print_header(
        f"Балістична таблиця — {bullet.name} | Нуль {args.zero} м | "
        f"Вітер {cross:+.1f} м/с"
    )
    print(f"  Темп: {atmo.temperature_c}°C  |  Тиск: {atmo.pressure_hpa} гПа  |"
          f"  Вологість: {atmo.humidity_pct}%  |  Висота: {atmo.altitude_m} м\n")

    headers = ["Дист, м", "Падіння, мм", "Знос вітром, мм",
               "Швидк, м/с", "Енергія, Дж", "ЧПС, с", "Mach"]
    table   = [[r["range_m"], r["drop_mm"], r["drift_mm"],
                r["velocity_ms"], r["energy_j"], r["tof_s"], r["mach"]]
               for r in rows]
    print(tabulate(table, headers=headers, tablefmt="rounded_outline",
                   floatfmt=".1f"))

    # Wind angular corrections
    if cross != 0.0:
        print("\n  Поправки на вітер (мрад / МОА / кліки ¼MOA):")
        corr_rows = []
        for r in rows:
            if r["range_m"] == 0:
                continue
            mrad_ = correction_mrad(r["drift_mm"] / 1000.0, r["range_m"])
            moa_  = mrad_to_moa(mrad_)
            clks  = moa_to_clicks(moa_)
            corr_rows.append([r["range_m"], f"{mrad_:+.2f}", f"{moa_:+.1f}", f"{clks:+.0f}"])
        print(tabulate(corr_rows,
                       headers=["Дист, м", "мрад", "МОА", "Кліки"],
                       tablefmt="simple"))
    print()


def cmd_traj(args) -> None:
    if args.preset:
        bullet = get_preset(args.preset)
    else:
        bullet = build_bullet_from_args(args)

    atmo  = build_atmo(args)
    use_g7 = getattr(args, "g7", False)
    elev   = getattr(args, "elev", None)

    if elev is None:
        elev = find_zero_elevation(bullet, atmo,
                                   zero_range_m=getattr(args, "zero", 100) or 100,
                                   use_g7=use_g7)
        print(f"\n  Кут підвищення (нуль {getattr(args,'zero',100)} м): "
              f"{elev:.4f} мрад = {mrad_to_moa(elev):.2f} МОА")

    pts = compute_trajectory(
        bullet, atmo,
        elevation_mrad=elev,
        max_range_m=getattr(args, "range", 1500) or 1500,
        step_m=getattr(args, "step", 100) or 100,
        wind_ms=getattr(args, "wind", 0) or 0.0,
        use_g7=use_g7,
    )

    print_header(f"Траєкторія — {bullet.name}  |  кут {elev:.3f} мрад")
    headers = ["Дист, м", "Висота, м", "Знос, м",
               "Vx, м/с", "Vy, м/с", "V заг, м/с", "Mach"]
    table   = [[round(p.x_m), round(p.y_m, 3), round(p.drift_m, 3),
                round(p.vx_ms, 1), round(p.vy_ms, 1),
                round(p.velocity_ms, 1), round(p.mach, 3)]
               for p in pts]
    print(tabulate(table, headers=headers, tablefmt="rounded_outline"))
    print()


def cmd_custom(args) -> None:
    """Interactive-style custom bullet → drop table."""
    bullet = build_bullet_from_args(args)
    atmo   = build_atmo(args)
    cross  = getattr(args, "wind", 0.0) or 0.0
    rows   = drop_table(
        bullet       = bullet,
        atmo         = atmo,
        zero_range_m = getattr(args, "zero", 100) or 100,
        max_range_m  = getattr(args, "range", 1000) or 1000,
        step_m       = getattr(args, "step", 50) or 50,
        wind_ms      = cross,
    )

    print_header(f"Custom bullet: {bullet.name}")
    headers = ["Дист, м", "Падіння, мм", "Знос вітром, мм",
               "Швидк, м/с", "Енергія, Дж", "ЧПС, с"]
    table   = [[r["range_m"], r["drop_mm"], r["drift_mm"],
                r["velocity_ms"], r["energy_j"], r["tof_s"]]
               for r in rows]
    print(tabulate(table, headers=headers, tablefmt="rounded_outline"))
    print()


# ── CLI parser ───────────────────────────────────────────────────────────────

def make_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="ballistic-calc",
        description="Балістичний калькулятор (точкова маса + атмосферний опір)",
    )
    sub = parser.add_subparsers(dest="command", required=True)

    # ── presets ──────────────────────────────────────────────────────
    sub.add_parser("presets", help="Список пресетів боєприпасів")

    # ── shared parent parsers ─────────────────────────────────────────
    bullet_parent = argparse.ArgumentParser(add_help=False)
    grp = bullet_parent.add_mutually_exclusive_group()
    grp.add_argument("--preset", metavar="NAME",   help="Назва пресету")
    bullet_parent.add_argument("--mass",     type=float, metavar="г",   help="Маса кулі (г)")
    bullet_parent.add_argument("--diameter", type=float, metavar="мм",  help="Калібр (мм)")
    bullet_parent.add_argument("--bc",       type=float, metavar="BC",  help="BC G1")
    bullet_parent.add_argument("--bc-g7",    type=float, metavar="BC7", dest="bc_g7", help="BC G7")
    bullet_parent.add_argument("--vel",      type=float, metavar="м/с", help="Початкова швидкість")
    bullet_parent.add_argument("--g7",  action="store_true", help="Використовувати G7 модель")

    atmo_parent = argparse.ArgumentParser(add_help=False)
    atmo_parent.add_argument("--temp",     type=float, default=15.0,    metavar="°C")
    atmo_parent.add_argument("--pressure", type=float, default=1013.25, metavar="гПа")
    atmo_parent.add_argument("--humidity", type=float, default=50.0,    metavar="%")
    atmo_parent.add_argument("--altitude", type=float, default=0.0,     metavar="м")

    range_parent = argparse.ArgumentParser(add_help=False)
    range_parent.add_argument("--zero",    type=float, default=100,  metavar="м", help="Нуль (м)")
    range_parent.add_argument("--range",   type=float, default=1000, metavar="м", help="Макс. дистанція")
    range_parent.add_argument("--step",    type=float, default=50,   metavar="м", help="Крок таблиці")
    range_parent.add_argument("--wind",    type=float, default=0.0,  metavar="м/с", help="Швидкість вітру")
    range_parent.add_argument("--wind-dir",type=float, default=None, metavar="°",  dest="wind_dir",
                              help="Метео-напрямок вітру (звідки, 0=N)")
    range_parent.add_argument("--heading", type=float, default=0.0,  metavar="°",
                              help="Азимут стрільби (0=N)")

    parents = [bullet_parent, atmo_parent, range_parent]

    # ── table ──────────────────────────────────────────────────────────
    p_table = sub.add_parser("table", parents=parents,
                              help="Балістична таблиця (падіння, знос вітром)")
    p_table.set_defaults(func=cmd_table)

    # ── custom ─────────────────────────────────────────────────────────
    p_cust = sub.add_parser("custom", parents=parents,
                             help="Власна куля — таблиця")
    p_cust.set_defaults(func=cmd_custom)

    # ── traj ───────────────────────────────────────────────────────────
    p_traj = sub.add_parser("traj", parents=parents,
                             help="Повна траєкторія з координатами")
    p_traj.add_argument("--elev", type=float, default=None, metavar="мрад",
                        help="Кут підвищення (мрад). Якщо не вказано — автонуль.")
    p_traj.set_defaults(func=cmd_traj)

    return parser


def main():
    parser = make_parser()
    args   = parser.parse_args()

    # Route to subcommand
    if args.command == "presets":
        cmd_presets(args)
    elif hasattr(args, "func"):
        args.func(args)
    else:
        parser.print_help()
        sys.exit(1)


if __name__ == "__main__":
    main()
