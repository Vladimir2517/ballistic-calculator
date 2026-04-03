using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    public sealed class MissionWizardForm : Form
    {
        private readonly PluginHost host;
        private readonly MissionWizardInput input = new MissionWizardInput();

        private readonly TabControl tabs = new TabControl();
        private readonly Button backButton = new Button();
        private readonly Button nextButton = new Button();
        private readonly Button generateButton = new Button();
        private readonly TextBox summaryBox = new TextBox();

        private NumericUpDown homeLat;
        private NumericUpDown homeLon;
        private NumericUpDown takeoffAlt;
        private NumericUpDown takeoffPitch;
        private NumericUpDown cruiseAlt;
        private NumericUpDown rtlAlt;

        private NumericUpDown areaCenterLat;
        private NumericUpDown areaCenterLon;
        private NumericUpDown areaWidth;
        private NumericUpDown areaHeight;
        private NumericUpDown laneSpacing;
        private NumericUpDown yaw;

        private NumericUpDown speed;
        private CheckBox addCamTrigger;
        private NumericUpDown triggerDist;
        private CheckBox loadDirectlyToFlightPlan;
        private CheckBox usePointRoute;
        private CheckBox useDeliveryTarget;
        private CheckBox deliveryOnlyMission;
        private NumericUpDown deliveryTargetLat;
        private NumericUpDown deliveryTargetLon;
        private NumericUpDown landingLat;
        private NumericUpDown landingLon;
        private NumericUpDown runInDistance;
        private CheckBox addPayloadRelease;
        private NumericUpDown payloadServo;
        private NumericUpDown payloadPwm;
        private NumericUpDown payloadDelay;
        private Button importBombingTableButton;
        private BombingTableSnapshot importedBombingTable;

        public MissionWizardForm(PluginHost host)
        {
            this.host = host;
            Text = "Майстер місії - покроково";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 920;
            Height = 640;
            MinimizeBox = false;
            MaximizeBox = false;

            tabs.Dock = DockStyle.Top;
            tabs.Height = 500;

            tabs.TabPages.Add(CreateStep1());
            tabs.TabPages.Add(CreateStep2());
            tabs.TabPages.Add(CreateStep3());
            tabs.TabPages.Add(CreateStep4());

            backButton.Text = "Назад";
            backButton.SetBounds(12, 520, 100, 32);
            backButton.Click += (_, __) => ChangeStep(-1);

            nextButton.Text = "Далі";
            nextButton.SetBounds(120, 520, 100, 32);
            nextButton.Click += (_, __) => ChangeStep(1);

            generateButton.Text = "Згенерувати місію";
            generateButton.SetBounds(740, 520, 150, 32);
            generateButton.Click += (_, __) => GenerateMission();

            Controls.Add(tabs);
            Controls.Add(backButton);
            Controls.Add(nextButton);
            Controls.Add(generateButton);

            UpdateButtons();

            if (MissionPointsStore.HasStart)
            {
                input.HomeLat = MissionPointsStore.StartLat;
                input.HomeLon = MissionPointsStore.StartLon;
            }

            if (MissionPointsStore.HasDelivery)
            {
                input.UsePointRoute = true;
                input.UseDeliveryTarget = true;
                input.HasDeliveryPoint = true;
                input.DeliveryTargetLat = MissionPointsStore.DeliveryLat;
                input.DeliveryTargetLon = MissionPointsStore.DeliveryLon;
            }

            if (MissionPointsStore.HasLanding)
            {
                input.HasLandingPoint = true;
                input.LandingLat = MissionPointsStore.LandingLat;
                input.LandingLon = MissionPointsStore.LandingLon;
            }

            MissionContextResolver.ApplyDefaults(host, input);

            TryAutoImportBombingTable();
            BuildSummary();
        }

        private TabPage CreateStep1()
        {
            var page = new TabPage("1. Старт і висота");

            homeLat = CreateNumeric(20, 40, -90, 90, input.HomeLat, 6, 0.000001M);
            homeLon = CreateNumeric(20, 90, -180, 180, input.HomeLon, 6, 0.000001M);
            takeoffAlt = CreateNumeric(20, 140, 5, 500, input.TakeoffAltMeters, 1, 1);
            takeoffPitch = CreateNumeric(20, 190, 12, 12, 12, 1, 1);
            takeoffPitch.ReadOnly = true;
            takeoffPitch.Increment = 0;
            cruiseAlt = CreateNumeric(20, 240, 10, 1000, input.CruiseAltMeters, 1, 1);
            rtlAlt = CreateNumeric(20, 290, 10, 500, input.RtlAltMeters, 1, 1);

            page.Controls.Add(CreateLabel("Широта старту", 20, 20));
            page.Controls.Add(homeLat);
            page.Controls.Add(CreateLabel("Довгота старту", 20, 70));
            page.Controls.Add(homeLon);
            page.Controls.Add(CreateLabel("Висота зльоту (м)", 20, 120));
            page.Controls.Add(takeoffAlt);
            page.Controls.Add(CreateLabel("Кут зльоту (град)", 20, 170));
            page.Controls.Add(takeoffPitch);
            page.Controls.Add(CreateLabel("Крейсерська висота (м)", 20, 220));
            page.Controls.Add(cruiseAlt);
            page.Controls.Add(CreateLabel("Висота RTL (м)", 20, 270));
            page.Controls.Add(rtlAlt);

            return page;
        }

        private TabPage CreateStep2()
        {
            var page = new TabPage("2. Зона обльоту");

            areaCenterLat = CreateNumeric(20, 40, -90, 90, input.AreaCenterLat, 6, 0.000001M);
            areaCenterLon = CreateNumeric(20, 90, -180, 180, input.AreaCenterLon, 6, 0.000001M);
            areaWidth = CreateNumeric(20, 140, 50, 20000, input.AreaWidthMeters, 1, 10);
            areaHeight = CreateNumeric(20, 190, 50, 20000, input.AreaHeightMeters, 1, 10);
            laneSpacing = CreateNumeric(20, 240, 5, 1000, input.LaneSpacingMeters, 1, 1);
            yaw = CreateNumeric(20, 290, -180, 180, input.YawDegrees, 1, 1);

            page.Controls.Add(CreateLabel("Широта центру зони", 20, 20));
            page.Controls.Add(areaCenterLat);
            page.Controls.Add(CreateLabel("Довгота центру зони", 20, 70));
            page.Controls.Add(areaCenterLon);
            page.Controls.Add(CreateLabel("Ширина зони (м)", 20, 120));
            page.Controls.Add(areaWidth);
            page.Controls.Add(CreateLabel("Висота зони (м)", 20, 170));
            page.Controls.Add(areaHeight);
            page.Controls.Add(CreateLabel("Крок між проходами (м)", 20, 220));
            page.Controls.Add(laneSpacing);
            page.Controls.Add(CreateLabel("Поворот шаблону (град)", 20, 270));
            page.Controls.Add(yaw);

            return page;
        }

        private TabPage CreateStep3()
        {
            var page = new TabPage("3. Дії та вантаж");

            speed = CreateNumeric(20, 40, 1, 60, input.SpeedMetersPerSecond, 1, 1);
            addCamTrigger = new CheckBox
            {
                Left = 20,
                Top = 95,
                Width = 280,
                Text = "Увімкнути тригер камери за дистанцією"
            };

            triggerDist = CreateNumeric(20, 145, 1, 500, input.CameraTriggerMeters, 1, 1);
            triggerDist.Enabled = false;
            addCamTrigger.CheckedChanged += (_, __) => triggerDist.Enabled = addCamTrigger.Checked;

            loadDirectlyToFlightPlan = new CheckBox
            {
                Left = 20,
                Top = 200,
                Width = 320,
                Checked = true,
                Text = "Завантажити згенеровану місію прямо у Flight Plan"
            };

            usePointRoute = new CheckBox
            {
                Left = 20,
                Top = 235,
                Width = 320,
                Checked = input.UsePointRoute,
                Text = "Використати маршрут за точками карти (Старт -> Доставка -> Посадка)"
            };

            useDeliveryTarget = new CheckBox
            {
                Left = 20,
                Top = 265,
                Width = 320,
                Checked = input.UseDeliveryTarget,
                Text = "Використати ціль доставки (взяти з карти)"
            };

            deliveryOnlyMission = new CheckBox
            {
                Left = 20,
                Top = 295,
                Width = 340,
                Checked = input.DeliveryOnlyMission,
                Text = "Лише доставка (пропустити шаблон обльоту)"
            };

            deliveryTargetLat = CreateNumeric(380, 235, -90, 90, input.DeliveryTargetLat, 6, 0.000001M);
            deliveryTargetLon = CreateNumeric(380, 285, -180, 180, input.DeliveryTargetLon, 6, 0.000001M);
            landingLat = CreateNumeric(380, 335, -90, 90, input.LandingLat, 6, 0.000001M);
            landingLon = CreateNumeric(380, 385, -180, 180, input.LandingLon, 6, 0.000001M);
            runInDistance = CreateNumeric(380, 335, 20, 2000, input.DeliveryRunInMeters, 1, 5);
            runInDistance.Top = 400;
            addPayloadRelease = new CheckBox
            {
                Left = 380,
                Top = 430,
                Width = 260,
                Checked = input.AddPayloadRelease,
                Text = "Додати команду скидання вантажу"
            };
            payloadServo = CreateNumeric(380, 460, 1, 16, input.PayloadServoNumber, 0, 1);
            payloadPwm = CreateNumeric(540, 460, 900, 2200, input.PayloadServoPwm, 0, 10);
            payloadDelay = CreateNumeric(700, 460, 0, 30, input.PayloadReleaseDelaySeconds, 1, 0.5M);

            importBombingTableButton = new Button
            {
                Left = 20,
                Top = 335,
                Width = 320,
                Height = 30,
                Text = "Імпорт параметрів з XLSX таблиці"
            };
            importBombingTableButton.Click += (_, __) => ImportBombingTableFromDialog();

            page.Controls.Add(CreateLabel("Швидкість (м/с)", 20, 20));
            page.Controls.Add(speed);
            page.Controls.Add(addCamTrigger);
            page.Controls.Add(CreateLabel("Дистанція тригера (м)", 20, 125));
            page.Controls.Add(triggerDist);
            page.Controls.Add(loadDirectlyToFlightPlan);
            page.Controls.Add(usePointRoute);
            page.Controls.Add(useDeliveryTarget);
            page.Controls.Add(deliveryOnlyMission);
            page.Controls.Add(importBombingTableButton);

            page.Controls.Add(CreateLabel("Широта точки доставки", 380, 215));
            page.Controls.Add(deliveryTargetLat);
            page.Controls.Add(CreateLabel("Довгота точки доставки", 380, 265));
            page.Controls.Add(deliveryTargetLon);
            page.Controls.Add(CreateLabel("Широта точки посадки", 380, 315));
            page.Controls.Add(landingLat);
            page.Controls.Add(CreateLabel("Довгота точки посадки", 380, 365));
            page.Controls.Add(landingLon);
            page.Controls.Add(CreateLabel("Дистанція заходу (м)", 380, 380));
            page.Controls.Add(runInDistance);
            page.Controls.Add(addPayloadRelease);
            page.Controls.Add(CreateLabel("Серво", 380, 440));
            page.Controls.Add(payloadServo);
            page.Controls.Add(CreateLabel("PWM", 540, 440));
            page.Controls.Add(payloadPwm);
            page.Controls.Add(CreateLabel("Затримка (с)", 700, 440));
            page.Controls.Add(payloadDelay);

            return page;
        }

        private TabPage CreateStep4()
        {
            var page = new TabPage("4. Перевірка і генерація");

            summaryBox.Multiline = true;
            summaryBox.ReadOnly = true;
            summaryBox.ScrollBars = ScrollBars.Vertical;
            summaryBox.Font = new Font("Consolas", 10);
            summaryBox.Dock = DockStyle.Fill;

            page.Controls.Add(summaryBox);
            return page;
        }

        private void ChangeStep(int delta)
        {
            PullUiValues();
            var next = tabs.SelectedIndex + delta;
            if (next < 0 || next >= tabs.TabPages.Count)
            {
                return;
            }

            tabs.SelectedIndex = next;
            BuildSummary();
            UpdateButtons();
        }

        private void PullUiValues()
        {
            input.HomeLat = (double)homeLat.Value;
            input.HomeLon = (double)homeLon.Value;
            input.TakeoffAltMeters = (float)takeoffAlt.Value;
            input.TakeoffPitchDegrees = 12f;
            input.CruiseAltMeters = (float)cruiseAlt.Value;
            input.RtlAltMeters = (float)rtlAlt.Value;

            input.AreaCenterLat = (double)areaCenterLat.Value;
            input.AreaCenterLon = (double)areaCenterLon.Value;
            input.AreaWidthMeters = (float)areaWidth.Value;
            input.AreaHeightMeters = (float)areaHeight.Value;
            input.LaneSpacingMeters = (float)laneSpacing.Value;
            input.YawDegrees = (float)yaw.Value;

            input.SpeedMetersPerSecond = (float)speed.Value;
            input.AddCameraTrigger = addCamTrigger.Checked;
            input.CameraTriggerMeters = (float)triggerDist.Value;

            input.UsePointRoute = usePointRoute.Checked;
            input.UseDeliveryTarget = useDeliveryTarget.Checked;
            input.DeliveryOnlyMission = deliveryOnlyMission.Checked;
            input.DeliveryTargetLat = (double)deliveryTargetLat.Value;
            input.DeliveryTargetLon = (double)deliveryTargetLon.Value;
            input.HasDeliveryPoint = input.UseDeliveryTarget;
            input.LandingLat = (double)landingLat.Value;
            input.LandingLon = (double)landingLon.Value;
            input.HasLandingPoint = input.UsePointRoute;
            input.DeliveryRunInMeters = (float)runInDistance.Value;
            input.AddPayloadRelease = addPayloadRelease.Checked;
            input.PayloadServoNumber = (int)payloadServo.Value;
            input.PayloadServoPwm = (int)payloadPwm.Value;
            input.PayloadReleaseDelaySeconds = (float)payloadDelay.Value;
        }

        private void BuildSummary()
        {
            PullUiValues();
            ApplyImportedBombingTable();
            MissionContextResolver.ApplyDefaults(host, input, false);

            var sb = new StringBuilder();
            sb.AppendLine("Підсумок майстра місії");
            sb.AppendLine(new string('=', 36));
            sb.AppendLine();
            sb.AppendLine($"Старт:           {input.HomeLat:F6}, {input.HomeLon:F6}");
            sb.AppendLine($"Висота зльоту:   {input.TakeoffAltMeters:F0} м");
            sb.AppendLine($"Кут зльоту:      {input.TakeoffPitchDegrees:F0} град");
            sb.AppendLine($"Крейсерська:     {input.CruiseAltMeters:F0} м");
            sb.AppendLine($"Висота RTL:      {input.RtlAltMeters:F0} м");
            sb.AppendLine();
            sb.AppendLine($"Центр зони:       {input.AreaCenterLat:F6}, {input.AreaCenterLon:F6}");
            sb.AppendLine($"Розмір зони:      {input.AreaWidthMeters:F0} x {input.AreaHeightMeters:F0} м");
            sb.AppendLine($"Крок проходів:    {input.LaneSpacingMeters:F0} м");
            sb.AppendLine($"Поворот:          {input.YawDegrees:F0} град");
            sb.AppendLine();
            sb.AppendLine($"Швидкість:        {input.SpeedMetersPerSecond:F0} м/с");
            sb.AppendLine($"Тригер камери:    {(input.AddCameraTrigger ? "УВІМК" : "ВИМК")}");
            if (input.AddCameraTrigger)
            {
                sb.AppendLine($"Дистанц. тригера: {input.CameraTriggerMeters:F0} м");
            }
            sb.AppendLine($"Маршрут за точк.: {(input.UsePointRoute ? "УВІМК" : "ВИМК")}");
            sb.AppendLine($"Ціль доставки:    {(input.UseDeliveryTarget ? "УВІМК" : "ВИМК")}");
            if (input.UseDeliveryTarget)
            {
                sb.AppendLine($"Точка доставки:   {input.DeliveryTargetLat:F6}, {input.DeliveryTargetLon:F6}");
                sb.AppendLine($"Відносна висота цілі: {input.DeliveryTargetRelativeAltMeters:F0} м");
                sb.AppendLine($"Висота скидання:  {input.DeliveryTargetRelativeAltMeters + input.DropHeightAboveTargetMeters:F0} м");
                sb.AppendLine($"Дистанц. заходу:  {input.DeliveryRunInMeters:F0} м");
                sb.AppendLine($"Відхід після скидання: {input.PostDropEgressMeters:F0} м по курсу заходу");
                sb.AppendLine($"Лише доставка:    {(input.DeliveryOnlyMission ? "ТАК" : "НІ")}");
                sb.AppendLine($"Скидання вантажу: {(input.AddPayloadRelease ? "УВІМК" : "ВИМК")}");
                if (input.AddPayloadRelease)
                {
                    sb.AppendLine($"Серво/PWM:       {input.PayloadServoNumber} / {input.PayloadServoPwm}");
                    sb.AppendLine($"Затримка:        {input.PayloadReleaseDelaySeconds:F1} с");
                }
            }
            if (input.UsePointRoute)
            {
                sb.AppendLine($"Точка посадки:    {input.LandingLat:F6}, {input.LandingLon:F6}");
                sb.AppendLine($"Заход на посадку: {input.LandingRunInMeters:F0} м");
            }

            sb.AppendLine($"Вітер:            {input.WindSpeedMps:F1} м/с з {input.WindDirectionFromDeg:F0}° ({input.WindSource})");
            if (importedBombingTable != null)
            {
                sb.AppendLine($"XLSX-джерело:      {Path.GetFileName(importedBombingTable.SourceFile)}");
            }

            try
            {
                var count = input.BuildMissionItems().Count;
                sb.AppendLine();
                sb.AppendLine($"Орієнтовна к-сть команд місії: {count}");
            }
            catch (Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine("Помилка перевірки: " + ex.Message);
            }

            summaryBox.Text = sb.ToString();
        }

        private void GenerateMission()
        {
            try
            {
                PullUiValues();
                MissionContextResolver.ApplyDefaults(host, input);
                ApplyImportedBombingTable();
                var mission = input.BuildMissionItems();

                var outputDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "MissionWizard");

                var outputFile = MissionBuilder.WriteQgcWpl(mission, outputDir);

                if (loadDirectlyToFlightPlan.Checked)
                {
                    MissionPlannerIntegration.LoadWaypointsIntoFlightPlanner(host, outputFile);
                }

                MessageBox.Show(
                    "Файл місії згенеровано:\n" + outputFile +
                    $"\n\nПісля точки скидання додано прямий відхід {input.PostDropEgressMeters:F0} м по курсу заходу." +
                    (loadDirectlyToFlightPlan.Checked
                        ? "\n\nМісію завантажено безпосередньо у Flight Plan."
                        : "\n\nІмпорт у Mission Planner:\nFlight Plan -> Load WP File"),
                    "Місію створено",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не вдалося згенерувати місію:\n" + ex.Message,
                    "Помилка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void UpdateButtons()
        {
            backButton.Enabled = tabs.SelectedIndex > 0;
            nextButton.Enabled = tabs.SelectedIndex < tabs.TabPages.Count - 1;
        }

        private void TryAutoImportBombingTable()
        {
            try
            {
                var candidatePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins", "Таблица_бомбометания.xlsx"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Таблица_бомбометания.xlsx"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MissionWizard", "Таблица_бомбометания.xlsx"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MissionWizard", "input", "Таблица_бомбометания.xlsx"),
                    @"C:\Projects\ballistic-calculator\input\Таблица_бомбометания.xlsx"
                };

                var found = candidatePaths.FirstOrDefault(File.Exists);
                if (string.IsNullOrEmpty(found))
                {
                    return;
                }

                if (BombingTableXlsx.TryLoad(found, out var snapshot, out _))
                {
                    importedBombingTable = snapshot;
                    ApplyImportedBombingTable();
                    PushInputToUi();
                }
            }
            catch
            {
                // Ignore auto-import errors to keep wizard startup fast and resilient.
            }
        }

        private void ImportBombingTableFromDialog()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Оберіть таблицю бомбометання (XLSX)";
                dialog.Filter = "Excel (*.xlsx)|*.xlsx";
                dialog.Multiselect = false;

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                if (!BombingTableXlsx.TryLoad(dialog.FileName, out var snapshot, out var error))
                {
                    MessageBox.Show(
                        "Не вдалося імпортувати таблицю:\n" + error,
                        "Імпорт XLSX",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                importedBombingTable = snapshot;
                ApplyImportedBombingTable();
                PushInputToUi();
                BuildSummary();

                MessageBox.Show(
                    "Параметри з таблиці застосовано.\n" +
                    "Буде використано координати цілі, швидкість, вітер, висоту бомбометання та віднос.",
                    "Імпорт XLSX",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void ApplyImportedBombingTable()
        {
            if (importedBombingTable == null)
            {
                return;
            }

            if (importedBombingTable.TargetLat.HasValue)
            {
                input.DeliveryTargetLat = importedBombingTable.TargetLat.Value;
            }
            if (importedBombingTable.TargetLon.HasValue)
            {
                input.DeliveryTargetLon = importedBombingTable.TargetLon.Value;
            }
            if (importedBombingTable.AircraftSpeedMps.HasValue && importedBombingTable.AircraftSpeedMps.Value > 0)
            {
                input.SpeedMetersPerSecond = importedBombingTable.AircraftSpeedMps.Value;
            }
            if (importedBombingTable.WindDirFromDeg.HasValue)
            {
                input.WindDirectionFromDeg = importedBombingTable.WindDirFromDeg.Value;
            }
            if (importedBombingTable.WindSpeedMps.HasValue && importedBombingTable.WindSpeedMps.Value >= 0)
            {
                input.WindSpeedMps = importedBombingTable.WindSpeedMps.Value;
            }
            if (importedBombingTable.DropHeightMeters.HasValue && importedBombingTable.DropHeightMeters.Value > 0)
            {
                input.DropHeightAboveTargetMeters = importedBombingTable.DropHeightMeters.Value;
            }
            if (importedBombingTable.ReleaseDistanceMeters.HasValue && importedBombingTable.ReleaseDistanceMeters.Value > 0)
            {
                input.DeliveryRunInMeters = importedBombingTable.ReleaseDistanceMeters.Value;
            }

            input.UseDeliveryTarget = true;
            input.HasDeliveryPoint = true;
            input.WindSource = "XLSX";
        }

        private void PushInputToUi()
        {
            SetNumeric(homeLat, input.HomeLat);
            SetNumeric(homeLon, input.HomeLon);
            SetNumeric(takeoffAlt, input.TakeoffAltMeters);
            SetNumeric(takeoffPitch, input.TakeoffPitchDegrees);
            SetNumeric(cruiseAlt, input.CruiseAltMeters);
            SetNumeric(rtlAlt, input.RtlAltMeters);

            SetNumeric(speed, input.SpeedMetersPerSecond);
            SetNumeric(deliveryTargetLat, input.DeliveryTargetLat);
            SetNumeric(deliveryTargetLon, input.DeliveryTargetLon);
            SetNumeric(runInDistance, input.DeliveryRunInMeters);

            useDeliveryTarget.Checked = input.UseDeliveryTarget;
        }

        private static void SetNumeric(NumericUpDown control, double value)
        {
            if (control == null)
            {
                return;
            }

            var val = (decimal)value;
            if (val < control.Minimum) val = control.Minimum;
            if (val > control.Maximum) val = control.Maximum;
            control.Value = val;
        }

        private static Label CreateLabel(string text, int x, int y)
        {
            return new Label { Text = text, Left = x, Top = y, Width = 260, Height = 18 };
        }

        private static NumericUpDown CreateNumeric(
            int x,
            int y,
            decimal min,
            decimal max,
            double initial,
            int decimals,
            decimal increment)
        {
            return new NumericUpDown
            {
                Left = x,
                Top = y,
                Width = 220,
                Minimum = min,
                Maximum = max,
                DecimalPlaces = decimals,
                Increment = increment,
                Value = Coerce((decimal)initial, min, max)
            };
        }

        private static decimal Coerce(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
