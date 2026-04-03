using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using MissionPlanner.Plugin;

namespace MissionWizardPlugin
{
    public sealed class PluginEntry : Plugin
    {
        private MissionMapPointController mapController;
        private ToolStripButton topBallisticsButton;
        private ToolStrip menuStripOwner;
        private Image ballisticsIcon;

        public override string Name => "Майстер місії";
        public override string Version => "0.1.0";
        public override string Author => "Vladimir2517";

        public override bool Init()
        {
            return true;
        }

        public override bool Loaded()
        {
            try
            {
                mapController = new MissionMapPointController(Host);
                TryAddTopMenuButton();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool Loop()
        {
            return true;
        }

        public override bool Exit()
        {
            if (topBallisticsButton != null)
            {
                topBallisticsButton.Click -= OnTopBallisticsClick;
                if (menuStripOwner != null && menuStripOwner.Items.Contains(topBallisticsButton))
                {
                    menuStripOwner.Items.Remove(topBallisticsButton);
                }
                topBallisticsButton.Dispose();
                topBallisticsButton = null;
                menuStripOwner = null;
            }

            if (ballisticsIcon != null)
            {
                ballisticsIcon.Dispose();
                ballisticsIcon = null;
            }

            if (mapController != null)
            {
                mapController.Dispose();
                mapController = null;
            }

            return true;
        }

        private void TryAddTopMenuButton()
        {
            try
            {
                var mainForm = Host?.MainForm;
                if (mainForm == null)
                {
                    return;
                }

                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var helpBtn = mainForm.GetType().GetField("MenuHelp", flags)?.GetValue(mainForm) as ToolStripItem;
                var plannerBtn = mainForm.GetType().GetField("MenuFlightPlanner", flags)?.GetValue(mainForm) as ToolStripItem;
                var dataBtn = mainForm.GetType().GetField("MenuFlightData", flags)?.GetValue(mainForm) as ToolStripItem;

                menuStripOwner = (helpBtn?.Owner as ToolStrip)
                    ?? (plannerBtn?.Owner as ToolStrip)
                    ?? (dataBtn?.Owner as ToolStrip);

                if (menuStripOwner == null)
                {
                    return;
                }

                ballisticsIcon = CreateBallisticsIcon();
                topBallisticsButton = new ToolStripButton("Балистика")
                {
                    DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                    Image = ballisticsIcon,
                    ImageTransparentColor = Color.Transparent,
                    ToolTipText = "Відкрити майстер балістики"
                };
                topBallisticsButton.Click += OnTopBallisticsClick;

                var insertIndex = GetInsertIndexAfterHelp(menuStripOwner, helpBtn);
                if (insertIndex >= 0)
                {
                    menuStripOwner.Items.Insert(insertIndex, topBallisticsButton);
                }
                else
                {
                    menuStripOwner.Items.Add(topBallisticsButton);
                }
            }
            catch
            {
                // If UI integration changes between Mission Planner versions, keep plugin functional.
            }
        }

        private static int GetInsertIndexAfterHelp(ToolStrip owner, ToolStripItem helpItem)
        {
            if (owner == null)
            {
                return -1;
            }

            if (helpItem != null)
            {
                var idx = owner.Items.IndexOf(helpItem);
                if (idx >= 0)
                {
                    return idx + 1;
                }
            }

            for (var i = 0; i < owner.Items.Count; i++)
            {
                var text = owner.Items[i].Text ?? string.Empty;
                if (text.Trim().Equals("HELP", StringComparison.OrdinalIgnoreCase))
                {
                    return i + 1;
                }
            }

            return -1;
        }

        private static Image CreateBallisticsIcon()
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            using (var penGrid = new Pen(Color.FromArgb(70, 95, 130), 1f))
            using (var penRing = new Pen(Color.FromArgb(35, 114, 186), 1.5f))
            using (var penArrow = new Pen(Color.FromArgb(220, 126, 33), 2f))
            using (var centerBrush = new SolidBrush(Color.FromArgb(220, 126, 33)))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                g.DrawLine(penGrid, 8, 1, 8, 15);
                g.DrawLine(penGrid, 1, 8, 15, 8);
                g.DrawEllipse(penRing, 2, 2, 12, 12);

                penArrow.EndCap = System.Drawing.Drawing2D.LineCap.ArrowAnchor;
                g.DrawLine(penArrow, 4, 12, 11, 5);
                g.FillEllipse(centerBrush, 6, 6, 4, 4);
            }

            return bmp;
        }

        private void OnTopBallisticsClick(object sender, EventArgs e)
        {
            try
            {
                WizardDialogService.OpenWizard(Host);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не вдалося відкрити вкладку Балистика:\n" + ex.Message,
                    "Балистика",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}
