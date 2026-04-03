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
            Text = "Mission Wizard - Step by Step";
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

            backButton.Text = "Back";
            backButton.SetBounds(12, 520, 100, 32);
            backButton.Click += (_, __) => ChangeStep(-1);

            nextButton.Text = "Next";
            nextButton.SetBounds(120, 520, 100, 32);
            nextButton.Click += (_, __) => ChangeStep(1);

            generateButton.Text = "Generate Mission";
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
            var page = new TabPage("1. Home and Altitude");

            homeLat = CreateNumeric(20, 40, -90, 90, input.HomeLat, 6, 0.000001M);
            homeLon = CreateNumeric(20, 90, -180, 180, input.HomeLon, 6, 0.000001M);
            takeoffAlt = CreateNumeric(20, 140, 5, 500, input.TakeoffAltMeters, 1, 1);
            cruiseAlt = CreateNumeric(20, 190, 10, 1000, input.CruiseAltMeters, 1, 1);
            rtlAlt = CreateNumeric(20, 240, 10, 300, input.RtlAltMeters, 1, 1);

            page.Controls.Add(CreateLabel("Home Latitude", 20, 20));
            page.Controls.Add(homeLat);
            page.Controls.Add(CreateLabel("Home Longitude", 20, 70));
            page.Controls.Add(homeLon);
            page.Controls.Add(CreateLabel("Takeoff Altitude (m)", 20, 120));
            page.Controls.Add(takeoffAlt);
            page.Controls.Add(CreateLabel("Cruise Altitude (m)", 20, 170));
            page.Controls.Add(cruiseAlt);
            page.Controls.Add(CreateLabel("RTL Altitude (m)", 20, 220));
            page.Controls.Add(rtlAlt);

            return page;
        }

        private TabPage CreateStep2()
        {
            var page = new TabPage("2. Survey Area");

            areaCenterLat = CreateNumeric(20, 40, -90, 90, input.AreaCenterLat, 6, 0.000001M);
            areaCenterLon = CreateNumeric(20, 90, -180, 180, input.AreaCenterLon, 6, 0.000001M);
            areaWidth = CreateNumeric(20, 140, 50, 20000, input.AreaWidthMeters, 1, 10);
            areaHeight = CreateNumeric(20, 190, 50, 20000, input.AreaHeightMeters, 1, 10);
            laneSpacing = CreateNumeric(20, 240, 5, 1000, input.LaneSpacingMeters, 1, 1);
            yaw = CreateNumeric(20, 290, -180, 180, input.YawDegrees, 1, 1);

            page.Controls.Add(CreateLabel("Area Center Latitude", 20, 20));
            page.Controls.Add(areaCenterLat);
            page.Controls.Add(CreateLabel("Area Center Longitude", 20, 70));
            page.Controls.Add(areaCenterLon);
            page.Controls.Add(CreateLabel("Area Width (m)", 20, 120));
            page.Controls.Add(areaWidth);
            page.Controls.Add(CreateLabel("Area Height (m)", 20, 170));
            page.Controls.Add(areaHeight);
            page.Controls.Add(CreateLabel("Lane Spacing (m)", 20, 220));
            page.Controls.Add(laneSpacing);
            page.Controls.Add(CreateLabel("Pattern Rotation (deg)", 20, 270));
            page.Controls.Add(yaw);

            return page;
        }

        private TabPage CreateStep3()
        {
            var page = new TabPage("3. Actions and Payload");

            speed = CreateNumeric(20, 40, 1, 60, input.SpeedMetersPerSecond, 1, 1);
            addCamTrigger = new CheckBox
            {
                Left = 20,
                Top = 95,
                Width = 280,
                Text = "Enable camera trigger by distance"
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
                Text = "Load generated mission directly to Flight Plan"
            };

            usePointRoute = new CheckBox
            {
                Left = 20,
                Top = 235,
                Width = 320,
                Checked = input.UsePointRoute,
                Text = "Use map points route (Start -> Delivery -> Landing)"
            };

            useDeliveryTarget = new CheckBox
            {
                Left = 20,
                Top = 265,
                Width = 320,
                Checked = input.UseDeliveryTarget,
                Text = "Use delivery target (set from map)"
            };

            deliveryOnlyMission = new CheckBox
            {
                Left = 20,
                Top = 295,
                Width = 340,
                Checked = input.DeliveryOnlyMission,
                Text = "Delivery only mission (skip survey pattern)"
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
                Text = "Add payload release command"
            };
            payloadServo = CreateNumeric(380, 460, 1, 16, input.PayloadServoNumber, 0, 1);
            payloadPwm = CreateNumeric(540, 460, 900, 2200, input.PayloadServoPwm, 0, 10);
            payloadDelay = CreateNumeric(700, 460, 0, 30, input.PayloadReleaseDelaySeconds, 1, 0.5M);

            page.Controls.Add(CreateLabel("Speed (m/s)", 20, 20));
            page.Controls.Add(speed);
            page.Controls.Add(addCamTrigger);
            page.Controls.Add(CreateLabel("Trigger Distance (m)", 20, 125));
            page.Controls.Add(triggerDist);
            page.Controls.Add(loadDirectlyToFlightPlan);
            page.Controls.Add(usePointRoute);
            page.Controls.Add(useDeliveryTarget);
            page.Controls.Add(deliveryOnlyMission);

            page.Controls.Add(CreateLabel("Delivery Target Latitude", 380, 215));
            page.Controls.Add(deliveryTargetLat);
            page.Controls.Add(CreateLabel("Delivery Target Longitude", 380, 265));
            page.Controls.Add(deliveryTargetLon);
            page.Controls.Add(CreateLabel("Landing Latitude", 380, 315));
            page.Controls.Add(landingLat);
            page.Controls.Add(CreateLabel("Landing Longitude", 380, 365));
            page.Controls.Add(landingLon);
            page.Controls.Add(CreateLabel("Run-in Distance (m)", 380, 380));
            page.Controls.Add(runInDistance);
            page.Controls.Add(addPayloadRelease);
            page.Controls.Add(CreateLabel("Servo", 380, 440));
            page.Controls.Add(payloadServo);
            page.Controls.Add(CreateLabel("PWM", 540, 440));
            page.Controls.Add(payloadPwm);
            page.Controls.Add(CreateLabel("Delay (s)", 700, 440));
            page.Controls.Add(payloadDelay);

            return page;
        }

        private TabPage CreateStep4()
        {
            var page = new TabPage("4. Review and Generate");

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
            sb.AppendLine("Mission Wizard Summary");
            sb.AppendLine(new string('=', 36));
            sb.AppendLine();
            sb.AppendLine($"Home:        {input.HomeLat:F6}, {input.HomeLon:F6}");
            sb.AppendLine($"Takeoff Alt: {input.TakeoffAltMeters:F0} m");
            sb.AppendLine($"Cruise Alt:  {input.CruiseAltMeters:F0} m");
            sb.AppendLine($"RTL Alt:     {input.RtlAltMeters:F0} m");
            sb.AppendLine();
            sb.AppendLine($"Area center: {input.AreaCenterLat:F6}, {input.AreaCenterLon:F6}");
            sb.AppendLine($"Area size:   {input.AreaWidthMeters:F0} x {input.AreaHeightMeters:F0} m");
            sb.AppendLine($"Lane space:  {input.LaneSpacingMeters:F0} m");
            sb.AppendLine($"Rotation:    {input.YawDegrees:F0} deg");
            sb.AppendLine();
            sb.AppendLine($"Speed:       {input.SpeedMetersPerSecond:F0} m/s");
            sb.AppendLine($"Camera trig: {(input.AddCameraTrigger ? "ON" : "OFF")}");
            if (input.AddCameraTrigger)
            {
                sb.AppendLine($"Trig dist:   {input.CameraTriggerMeters:F0} m");
            }
            sb.AppendLine($"Point route: {(input.UsePointRoute ? "ON" : "OFF")}");
            sb.AppendLine($"Use target:  {(input.UseDeliveryTarget ? "ON" : "OFF")}");
            if (input.UseDeliveryTarget)
            {
                sb.AppendLine($"Target:      {input.DeliveryTargetLat:F6}, {input.DeliveryTargetLon:F6}");
                sb.AppendLine($"Run-in:      {input.DeliveryRunInMeters:F0} m");
                sb.AppendLine($"DeliveryOnly:{(input.DeliveryOnlyMission ? "YES" : "NO")}");
                sb.AppendLine($"Release cmd: {(input.AddPayloadRelease ? "ON" : "OFF")}");
                if (input.AddPayloadRelease)
                {
                    sb.AppendLine($"Servo/PWM:   {input.PayloadServoNumber} / {input.PayloadServoPwm}");
                    sb.AppendLine($"Delay:       {input.PayloadReleaseDelaySeconds:F1} s");
                }
            }
            if (input.UsePointRoute)
            {
                sb.AppendLine($"Landing:     {input.LandingLat:F6}, {input.LandingLon:F6}");
            }

            try
            {
                var count = input.BuildMissionItems().Count;
                sb.AppendLine();
                sb.AppendLine($"Estimated mission items: {count}");
            }
            catch (Exception ex)
            {
                sb.AppendLine();
                sb.AppendLine("Validation error: " + ex.Message);
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
                    "Mission file generated:\n" + outputFile +
                    (loadDirectlyToFlightPlan.Checked
                        ? "\n\nMission was loaded directly into Flight Plan."
                        : "\n\nImport it in Mission Planner:\nFlight Plan -> Load WP File"),
                    "Mission Ready",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Mission generation failed:\n" + ex.Message,
                    "Error",
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
