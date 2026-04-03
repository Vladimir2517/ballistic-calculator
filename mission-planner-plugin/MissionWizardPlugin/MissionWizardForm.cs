using System;
using System.Drawing;
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

            BuildSummary();
        }

        private TabPage CreateStep1()
        {
            var page = new TabPage("1. Старт і висота");

            homeLat = CreateNumeric(20, 40, -90, 90, input.HomeLat, 6, 0.000001M);
            homeLon = CreateNumeric(20, 90, -180, 180, input.HomeLon, 6, 0.000001M);
            takeoffAlt = CreateNumeric(20, 140, 5, 500, input.TakeoffAltMeters, 1, 1);
            cruiseAlt = CreateNumeric(20, 190, 10, 1000, input.CruiseAltMeters, 1, 1);
            rtlAlt = CreateNumeric(20, 240, 10, 300, input.RtlAltMeters, 1, 1);

            page.Controls.Add(CreateLabel("Широта старту", 20, 20));
            page.Controls.Add(homeLat);
            page.Controls.Add(CreateLabel("Довгота старту", 20, 70));
            page.Controls.Add(homeLon);
            page.Controls.Add(CreateLabel("Висота зльоту (м)", 20, 120));
            page.Controls.Add(takeoffAlt);
            page.Controls.Add(CreateLabel("Крейсерська висота (м)", 20, 170));
            page.Controls.Add(cruiseAlt);
            page.Controls.Add(CreateLabel("Висота RTL (м)", 20, 220));
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

            page.Controls.Add(CreateLabel("Швидкість (м/с)", 20, 20));
            page.Controls.Add(speed);
            page.Controls.Add(addCamTrigger);
            page.Controls.Add(CreateLabel("Дистанція тригера (м)", 20, 125));
            page.Controls.Add(triggerDist);
            page.Controls.Add(loadDirectlyToFlightPlan);
            page.Controls.Add(usePointRoute);
            page.Controls.Add(useDeliveryTarget);
            page.Controls.Add(deliveryOnlyMission);

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

            var sb = new StringBuilder();
            sb.AppendLine("Підсумок майстра місії");
            sb.AppendLine(new string('=', 36));
            sb.AppendLine();
            sb.AppendLine($"Старт:           {input.HomeLat:F6}, {input.HomeLon:F6}");
            sb.AppendLine($"Висота зльоту:   {input.TakeoffAltMeters:F0} м");
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
                sb.AppendLine($"Дистанц. заходу:  {input.DeliveryRunInMeters:F0} м");
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
