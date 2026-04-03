# Troubleshooting и типовые проблемы

## 1) `python main.py ...` не запускается

Проверьте, что зависимости установлены:

```bash
pip install -r requirements.txt
```

## 2) Нет модуля `tabulate`

```bash
pip install tabulate
```

## 3) Ошибка кодировки в Windows-консоли

Если в терминале появляются ошибки вывода Unicode, используйте UTF-8:

```powershell
chcp 65001
```

И/или настройте переменную:

```powershell
$env:PYTHONIOENCODING="utf-8"
```

## 4) `gh auth status` показывает, что вы не вошли

Повторите вход:

```bash
gh auth login --hostname github.com --git-protocol https --web
```

## 5) Результаты сильно расходятся с реальностью

Проверьте:
- корректность BC (G1 vs G7)
- фактическую начальную скорость
- атмосферу (давление/температура/высота)
- корректность направления ветра (`--wind-dir`, `--heading`)
