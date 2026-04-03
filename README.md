# Ballistic Calculator

Балістичний калькулятор на Python із моделлю точкової маси, атмосферним опором (G1/G7), корекцією на вітер та таблицями падіння.

## Можливості

| Функція | Деталі |
|---|---|
| Фізична модель | Точкова маса + опір повітря (G1/G7 drag functions), RK4 інтегратор |
| Атмосфера | Температура, тиск, вологість, висота → реальна щільність повітря |
| Вітер | Поперечна та метеорологічна корекція, автоматичні поправки (мрад/МОА/кліки) |
| Пресети | 10 пресетів: 5.45×39, 5.56×45, 7.62×39, 7.62×54R, .308 Win, .338 Lapua, .50 BMG, 12.7×108, 82 мм міна, 120 мм міна |
| Вивід | Таблиці падіння, знос вітром, швидкість, енергія, ЧПС, Mach; повна траєкторія (x/y/vx/vy) |

## Встановлення

```bash
pip install -r requirements.txt
```

## Використання

### Список пресетів
```bash
python main.py presets
```

### Таблиця падіння (пресет)
```bash
# 7.62×54R, нуль 300 м, вітер 5 м/с, до 1000 м
python main.py table --preset "7.62x54R B32" --zero 300 --range 1000 --wind 5
```

### Таблиця з метеорологічними умовами
```bash
python main.py table \
  --preset ".308 Win 175gr Sierra" \
  --zero 200 --range 1000 --step 25 \
  --wind 8 --wind-dir 270 --heading 0 \
  --temp 5 --pressure 980 --altitude 500
```

### Власна куля
```bash
python main.py custom \
  --mass 11.34 --diameter 7.82 --bc 0.505 --vel 805 \
  --zero 100 --range 800 --wind 3
```

### Повна траєкторія
```bash
python main.py traj --preset "5.56x45 M855" --zero 100 --range 600 --step 50
```

### G7 модель (boat-tail кулі)
```bash
python main.py table --preset ".338 Lapua 250gr" --zero 300 --range 1500 --wind 10 --g7
```

## Структура проекту

```
ballistic-calculator/
├── main.py                   # CLI точка входу
├── requirements.txt
└── calculator/
    ├── ballistics.py          # Фізичний рушій (траєкторія, RK4, drag)
    ├── presets.py             # Пресети боєприпасів
    └── wind.py                # Корекція вітру, кутові поправки
```

## Фізична модель

Рівняння руху (у площині XY + дрейф Z):

$$\ddot{x} = -\frac{F_d}{m} \cdot \frac{v_x}{v}, \quad \ddot{y} = -\frac{F_d}{m} \cdot \frac{v_y}{v} - g, \quad \ddot{z} = -\frac{F_d}{m} \cdot \frac{v_z - w}{v}$$

де $F_d = \frac{1}{2} \rho v^2 C_d(M) A$, $M$ — число Маха, $C_d$ — коефіцієнт опору G1/G7.

Інтегрування методом Рунге-Кутта 4-го порядку, крок $\Delta t = 1$ мс.

## Ліцензія

MIT
